using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        internal sealed class GuiStoredValue
        {
            internal readonly GuiValueKind Kind;
            internal readonly string Text;
            internal readonly bool Boolean;
            internal readonly int Integer;
            internal readonly GuiImageSourceValue ImageSource;
            private readonly double[] NumberValues;
            internal double[] Numbers { get { return NumberValues == null ? null : (double[])NumberValues.Clone(); } }

            private GuiStoredValue(GuiValueKind Kind, string Text = null, bool Boolean = false, int Integer = 0,
                GuiImageSourceValue ImageSource = null, params double[] Numbers)
            { this.Kind = Kind; this.Text = Text; this.Boolean = Boolean; this.Integer = Integer; this.ImageSource = ImageSource; NumberValues = Numbers; }
            internal static GuiStoredValue String(string Value) { return new GuiStoredValue(GuiValueKind.String, Text: Value); }
            internal static GuiStoredValue Bool(bool Value) { return new GuiStoredValue(GuiValueKind.Boolean, Boolean: Value); }
            internal static GuiStoredValue Int(int Value) { return new GuiStoredValue(GuiValueKind.Integer, Integer: Value); }
            internal static GuiStoredValue Number(double Value) { return new GuiStoredValue(GuiValueKind.Number, Numbers: new[] {Normalize(Value)}); }
            internal static GuiStoredValue UDim(double Scale, double Offset) { return new GuiStoredValue(GuiValueKind.UDim, Numbers: new[] {Normalize(Scale), Normalize(Offset)}); }
            internal static GuiStoredValue UDim2(double XScale, double XOffset, double YScale, double YOffset)
            { return new GuiStoredValue(GuiValueKind.UDim2, Numbers: new[] {Normalize(XScale), Normalize(XOffset), Normalize(YScale), Normalize(YOffset)}); }
            internal static GuiStoredValue Vector2(double X, double Y) { return new GuiStoredValue(GuiValueKind.Vector2, Numbers: new[] {Normalize(X), Normalize(Y)}); }
            internal static GuiStoredValue Color3(double R, double G, double B) { return new GuiStoredValue(GuiValueKind.Color3, Numbers: new[] {Normalize(R), Normalize(G), Normalize(B)}); }
            internal static GuiStoredValue Image(GuiImageSourceValue Value) { return new GuiStoredValue(GuiValueKind.ImageSource, ImageSource: Value); }
            private static double Normalize(double Value) { return Value == 0 ? 0 : Value; }

            internal string[] Encode()
            {
                switch (Kind) {
                    case GuiValueKind.String: return new[] {"string", Text};
                    case GuiValueKind.Boolean: return new[] {"boolean", Boolean ? "1" : "0"};
                    case GuiValueKind.Integer: return new[] {"integer", Integer.ToString(CultureInfo.InvariantCulture)};
                    case GuiValueKind.Number: return new[] {"number", Format(NumberValues[0])};
                    case GuiValueKind.UDim: return new[] {"udim", Format(NumberValues[0]), Format(NumberValues[1])};
                    case GuiValueKind.UDim2: return new[] {"udim2", Format(NumberValues[0]), Format(NumberValues[1]), Format(NumberValues[2]), Format(NumberValues[3])};
                    case GuiValueKind.Vector2: return new[] {"vector2", Format(NumberValues[0]), Format(NumberValues[1])};
                    case GuiValueKind.Color3: return new[] {"color3", Format(NumberValues[0]), Format(NumberValues[1]), Format(NumberValues[2])};
                    case GuiValueKind.ImageSource: return ImageSource.Encode();
                    default: throw new FacadeException("unsupported GUI value kind");
                }
            }
            internal bool SameAs(GuiStoredValue Other)
            {
                if (Other == null || Kind != Other.Kind || Text != Other.Text || Boolean != Other.Boolean || Integer != Other.Integer ||
                    !Object.Equals(ImageSource, Other.ImageSource)) return false;
                if (NumberValues == null || Other.NumberValues == null) return NumberValues == Other.NumberValues;
                if (NumberValues.Length != Other.NumberValues.Length) return false;
                for (int Index = 0; Index < NumberValues.Length; ++Index) if (NumberValues[Index] != Other.NumberValues[Index]) return false;
                return true;
            }
            private static string Format(double Value) { return Value.ToString("R", CultureInfo.InvariantCulture); }
        }

        internal sealed class GuiRetainedNode
        {
            internal readonly GuiObjectIdentity Identity;
            internal readonly GuiClassId ClassId;
            internal ulong? ParentId;
            internal readonly List<ulong> Children = new List<ulong>();
            internal readonly Dictionary<GuiPropertyId, GuiStoredValue> Properties = new Dictionary<GuiPropertyId, GuiStoredValue>();
            internal GuiRetainedNode(GuiObjectIdentity Identity, GuiClassId ClassId) { this.Identity = Identity; this.ClassId = ClassId; }
            internal GuiRetainedNode Copy()
            {
                var Result = new GuiRetainedNode(Identity, ClassId) {ParentId = ParentId};
                Result.Children.AddRange(Children); foreach (var Value in Properties) Result.Properties.Add(Value.Key, Value.Value);
                return Result;
            }
        }

        internal sealed class GuiRetainedState
        {
            internal readonly Dictionary<ulong, GuiRetainedNode> Nodes = new Dictionary<ulong, GuiRetainedNode>();
            internal readonly Dictionary<string, ulong> Connections = new Dictionary<string, ulong>(StringComparer.Ordinal);
            internal readonly Dictionary<string, GuiPresentation> Presentations = new Dictionary<string, GuiPresentation>(StringComparer.Ordinal);
            internal readonly Dictionary<ulong, GuiScreenSynchronization> Synchronization = new Dictionary<ulong, GuiScreenSynchronization>();
            internal readonly Queue<GuiBackendTarget> PendingDestroys = new Queue<GuiBackendTarget>();
            internal int Screens;
            internal GuiRetainedState Copy()
            {
                var Result = new GuiRetainedState {Screens = Screens};
                foreach (var Value in Nodes) Result.Nodes.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in Connections) Result.Connections.Add(Value.Key, Value.Value);
                foreach (var Value in Presentations) Result.Presentations.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in Synchronization) Result.Synchronization.Add(Value.Key, Value.Value.Copy());
                foreach (GuiBackendTarget Value in PendingDestroys) Result.PendingDestroys.Enqueue(Value);
                return Result;
            }
        }

        internal sealed class GuiRetainedWorld
        {
            internal const string ActionCommand = "carbonluau.gui.action";
            internal readonly GuiLimits Limits;
            internal readonly PlayerDirectory Players;
            internal readonly IGuiBackend Backend;
            internal readonly GuiActionDiagnostics ActionDiagnostics = new GuiActionDiagnostics();
            internal readonly GuiRuntimeDiagnostics RuntimeDiagnostics = new GuiRuntimeDiagnostics();
            internal int LiveObjects { get; private set; }
            internal int LivePresentations { get; private set; }
            private readonly HashSet<GuiRetainedRegistry> Registries = new HashSet<GuiRetainedRegistry>();
            private readonly Dictionary<string, int> PlayerPresentations = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, GuiActionRecord> Actions = new Dictionary<string, GuiActionRecord>(StringComparer.Ordinal);
            private readonly Dictionary<GuiRetainedRegistry, int> DomainActions = new Dictionary<GuiRetainedRegistry, int>();
            private readonly Dictionary<string, GuiPlayerActionRate> PlayerActionRates = new Dictionary<string, GuiPlayerActionRate>(StringComparer.Ordinal);
            private readonly Dictionary<string, GuiRetiredAction> RetiredActions = new Dictionary<string, GuiRetiredAction>(StringComparer.Ordinal);
            private readonly Queue<string> RetiredActionOrder = new Queue<string>();
            internal int LiveActionCount { get { return Actions.Count; } }
            internal int RetiredActionCount { get { return RetiredActions.Count; } }
            internal int LiveRegistryCount { get { return Registries.Count; } }
            internal int PlayerActionRateCount { get { return PlayerActionRates.Count; } }
            internal int DomainActionOwnerCount { get { return DomainActions.Count; } }
            internal GuiRetainedWorld(GuiLimits Limits) : this(Limits, null, new InMemoryGuiBackend()) { }
            internal GuiRetainedWorld(GuiLimits Limits, PlayerDirectory Players, IGuiBackend Backend)
            { this.Limits = Limits ?? throw new ArgumentNullException("Limits"); this.Players = Players; this.Backend = Backend ?? throw new ArgumentNullException("Backend"); }
            internal void RegisterRegistry(GuiRetainedRegistry Registry)
            {
                if (Registry == null || !Registries.Add(Registry)) throw new FacadeException("invalid GUI registry publication");
            }
            internal void UnregisterRegistry(GuiRetainedRegistry Registry) { if (Registry != null) Registries.Remove(Registry); }
            internal void Adjust(int Change)
            {
                long Next = (long)LiveObjects + Change;
                if (Next < 0 || Next > Limits.MaxObjectsGlobal) throw new FacadeException("global GUI object limit reached");
                LiveObjects = (int)Next;
            }
            internal void Restore(int Change)
            {
                long Next = (long)LiveObjects + Change;
                if (Next < 0 || Next > Int32.MaxValue) throw new FacadeException("invalid GUI publication restoration");
                LiveObjects = (int)Next;
            }
            internal void AddPresentation(string PlayerToken)
            {
                int PlayerCount; PlayerPresentations.TryGetValue(PlayerToken, out PlayerCount);
                if (LivePresentations >= Limits.MaxPresentationsGlobal) throw new FacadeException("global GUI presentation limit reached");
                if (PlayerCount >= Limits.MaxScreensPerPlayerConnection) throw new FacadeException("Player GUI presentation limit reached");
                LivePresentations++; PlayerPresentations[PlayerToken] = PlayerCount + 1;
            }
            internal void RemovePresentation(string PlayerToken)
            {
                int PlayerCount;
                if (LivePresentations <= 0 || !PlayerPresentations.TryGetValue(PlayerToken, out PlayerCount) || PlayerCount <= 0)
                    throw new FacadeException("invalid GUI presentation accounting");
                LivePresentations--; if (PlayerCount == 1) PlayerPresentations.Remove(PlayerToken); else PlayerPresentations[PlayerToken] = PlayerCount - 1;
            }
            internal void RestorePresentations(IEnumerable<GuiPresentation> Current, IEnumerable<GuiPresentation> Previous)
            {
                foreach (GuiPresentation Value in Current) RemovePresentation(Value.PlayerToken);
                foreach (GuiPresentation Value in Previous) {
                    int Count; PlayerPresentations.TryGetValue(Value.PlayerToken, out Count);
                    LivePresentations++; PlayerPresentations[Value.PlayerToken] = Count + 1;
                }
            }
            internal PlayerView Resolve(string Token, string UserId)
            { return Players == null ? null : Players.Resolve(Token, UserId); }
            internal PlayerLifetime ExactPlayer(string Token, string UserId)
            {
                if (Players == null) return null;
                PlayerLifetime Result = Players.Find(UserId);
                return Result != null && Result.Token == Token ? Result : null;
            }
            internal string NewClientPrefix()
            {
                var Bytes = new byte[16]; using (RandomNumberGenerator Random = RandomNumberGenerator.Create()) Random.GetBytes(Bytes);
                return "cluau_" + BitConverter.ToString(Bytes).Replace("-", "").ToLowerInvariant() + "_";
            }

            internal string NewActionToken(HashSet<string> Pending)
            {
                for (int Attempt = 0; Attempt < 16; ++Attempt) {
                    var Bytes = new byte[16]; using (RandomNumberGenerator Random = RandomNumberGenerator.Create()) Random.GetBytes(Bytes);
                    string Token = BitConverter.ToString(Bytes).Replace("-", "").ToLowerInvariant();
                    if (!Actions.ContainsKey(Token) && !RetiredActions.ContainsKey(Token) && (Pending == null || !Pending.Contains(Token))) return Token;
                }
                throw new FacadeException("GUI action identity generation failed");
            }

            internal void ActivateActions(GuiRetainedRegistry Registry, IList<GuiActionRecord> Values)
            {
                if (Values == null || Values.Count == 0) return;
                int DomainCount; DomainActions.TryGetValue(Registry, out DomainCount);
                if (Values.Count > Limits.MaxActionTokensPerPresentation || DomainCount > Limits.MaxActionTokensPerDomain - Values.Count ||
                    Actions.Count > Limits.MaxActionTokensGlobal - Values.Count)
                    throw new FacadeException("GUI action token limit reached");
                foreach (GuiActionRecord Value in Values) if (Value == null || Value.Registry != Registry || Actions.ContainsKey(Value.Token))
                    throw new FacadeException("invalid GUI action publication");
                long Now = Stopwatch.GetTimestamp();
                foreach (GuiActionRecord Value in Values) {
                    Actions.Add(Value.Token, Value);
                    GuiPlayerActionRate PlayerRate;
                    if (!PlayerActionRates.TryGetValue(Value.PlayerToken, out PlayerRate)) {
                        PlayerRate = new GuiPlayerActionRate(Limits, Now); PlayerActionRates.Add(Value.PlayerToken, PlayerRate);
                    }
                    PlayerRate.References++;
                }
                DomainActions[Registry] = DomainCount + Values.Count;
            }

            internal void EnsureActionCapacity(GuiRetainedRegistry Registry, int Count)
            {
                int DomainCount; DomainActions.TryGetValue(Registry, out DomainCount);
                if (Count < 0 || Count > Limits.MaxActionTokensPerPresentation || DomainCount > Limits.MaxActionTokensPerDomain - Count ||
                    Actions.Count > Limits.MaxActionTokensGlobal - Count) throw new FacadeException("GUI action token limit reached");
            }

            internal void InvalidateAction(string Token, GuiActionRejection Reason)
            {
                GuiActionRecord Value;
                if (!Actions.TryGetValue(Token, out Value)) return;
                Actions.Remove(Token);
                int DomainCount;
                if (DomainActions.TryGetValue(Value.Registry, out DomainCount)) {
                    if (DomainCount <= 1) DomainActions.Remove(Value.Registry); else DomainActions[Value.Registry] = DomainCount - 1;
                }
                GuiPlayerActionRate PlayerRate;
                if (PlayerActionRates.TryGetValue(Value.PlayerToken, out PlayerRate) && --PlayerRate.References <= 0)
                    PlayerActionRates.Remove(Value.PlayerToken);
                Retire(Token, Reason);
            }

            internal void ReconcileActions(GuiRetainedRegistry Registry, HashSet<string> Keep, GuiActionRejection Reason)
            {
                var Remove = new List<string>();
                foreach (var Value in Actions) if (Value.Value.Registry == Registry && (Keep == null || !Keep.Contains(Value.Key))) Remove.Add(Value.Key);
                foreach (string Token in Remove) InvalidateAction(Token, Reason);
            }

            private void Retire(string Token, GuiActionRejection Reason)
            {
                if (RetiredActions.ContainsKey(Token)) return;
                while (RetiredActions.Count >= Limits.MaxActionTokensGlobal && RetiredActionOrder.Count != 0)
                    RetiredActions.Remove(RetiredActionOrder.Dequeue());
                RetiredActions.Add(Token, new GuiRetiredAction(Reason)); RetiredActionOrder.Enqueue(Token);
            }

            internal bool TryAdmit(PlayerLifetime Sender, string Token, out GuiActionAdmission Admission)
            {
                Admission = null;
                if (!CanonicalToken(Token)) { ActionDiagnostics.Reject(GuiActionRejection.Malformed); return false; }
                GuiActionRecord Record;
                if (!Actions.TryGetValue(Token, out Record)) {
                    GuiRetiredAction Retired;
                    ActionDiagnostics.Reject(RetiredActions.TryGetValue(Token, out Retired) ? Retired.Reason : GuiActionRejection.Unknown);
                    return false;
                }
                PlayerView Current = Sender == null || Players == null ? null : Players.Resolve(Sender.Token, Sender.UserId);
                if (Current == null || Sender.Token != Record.PlayerToken || Sender.UserId != Record.PlayerUserId ||
                    !Object.ReferenceEquals(Sender.Identity, Record.PlayerIdentity) || !Object.ReferenceEquals(Sender.Connection, Record.PlayerConnection)) {
                    ActionDiagnostics.Reject(GuiActionRejection.CrossPlayer); return false;
                }
                string[] Registrations; GuiActionRejection Reason = Record.Registry.ValidateAction(Record, null, out Registrations);
                if (Reason != GuiActionRejection.None) { ActionDiagnostics.Reject(Reason); return false; }
                long Now = Stopwatch.GetTimestamp(); GuiPlayerActionRate PlayerRate;
                if (!PlayerActionRates.TryGetValue(Record.PlayerToken, out PlayerRate) || !Record.Rate.Allow(Now) || !PlayerRate.Rate.Allow(Now)) {
                    ActionDiagnostics.Reject(GuiActionRejection.RateLimited); return false;
                }
                Admission = new GuiActionAdmission(Record, Registrations); return true;
            }

            internal bool ValidateQueued(string Token, string Registration, string PlayerToken, string PlayerUserId,
                ulong ScreenId, ulong Epoch, ulong ButtonId)
            {
                GuiActionRecord Record; string[] Ignored;
                bool Valid = Actions.TryGetValue(Token, out Record) && Record.PlayerToken == PlayerToken && Record.PlayerUserId == PlayerUserId &&
                    Record.ScreenId == ScreenId && Record.PresentationEpoch == Epoch && Record.ButtonId == ButtonId &&
                    Record.Registry.ValidateAction(Record, Registration, out Ignored) == GuiActionRejection.None;
                if (!Valid) ActionDiagnostics.Reject(GuiActionRejection.PreEntryStale);
                return Valid;
            }

            internal void Accepted() { ActionDiagnostics.Accept(); }
            internal void QueueRejected() { ActionDiagnostics.Reject(GuiActionRejection.QueueFull); }
            internal string ActionStatus { get { return ActionDiagnostics.Status() + "; active/retired: " + Actions.Count + "/" + RetiredActions.Count; } }
            internal void BackendAccepted(bool Full) { RuntimeDiagnostics.BackendAccepted(Full); }
            internal void BackendFailed() { RuntimeDiagnostics.BackendFailed(); }
            internal void RecordResourceLimit(Exception Error)
            {
                string Message = Error == null ? "" : Error.Message ?? "";
                if (Message.IndexOf("limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Message.IndexOf("bound", StringComparison.OrdinalIgnoreCase) >= 0)
                    RuntimeDiagnostics.ResourceLimitRejected();
            }
            internal string Status
            {
                get {
                    int Screens = 0, Connections = 0, Dirty = 0, FullResync = 0, Blocked = 0, PendingDestroys = 0;
                    foreach (GuiRetainedRegistry Registry in Registries) {
                        Screens += Registry.ScreenCount; Connections += Registry.ConnectionCount;
                        Dirty += Registry.DirtyPresentationCount; FullResync += Registry.FullResyncPresentationCount;
                        Blocked += Registry.BlockedPresentationCount; PendingDestroys += Registry.PendingDestroyCount;
                    }
                    return "GUI live registries/objects/screens/presentations/connections/actions: " + Registries.Count + "/" +
                        LiveObjects + "/" + Screens + "/" + LivePresentations + "/" + Connections + "/" + Actions.Count +
                        "\nGUI presentation dirty/full-resync/blocked/pending-destroy: " + Dirty + "/" + FullResync + "/" + Blocked + "/" + PendingDestroys +
                        "\nGUI backend full/patch/failures; resource-limit rejections: " + RuntimeDiagnostics.FullRebuilds + "/" +
                        RuntimeDiagnostics.Patches + "/" + RuntimeDiagnostics.BackendSendFailures + "; " + RuntimeDiagnostics.ResourceLimitRejections +
                        "\n" + ActionStatus;
                }
            }
            private static bool CanonicalToken(string Token)
            {
                if (Token == null || Token.Length != 32) return false;
                foreach (char Value in Token) if (!((Value >= '0' && Value <= '9') || (Value >= 'a' && Value <= 'f'))) return false;
                return true;
            }
        }

        internal sealed class GuiRetainedRegistry : IGuiRetainedRegistry, IDisposable
        {
            private readonly GuiRetainedWorld World;
            private readonly GuiLimits Limits;
            private readonly ulong VmGenerationId, DomainLifetimeId;
            private readonly Stack<GuiRetainedState> Publications = new Stack<GuiRetainedState>();
            private GuiRetainedState State = new GuiRetainedState();
            private ulong NextObjectId = 1;
            private ulong NextPresentationEpoch = 1;
            private string PresentationFlushCursor;
            private bool PreferDestroy;
            private bool Disposed;

            internal GuiRetainedRegistry(GuiRetainedWorld World, ulong VmGenerationId, ulong DomainLifetimeId)
            {
                if (VmGenerationId == 0 || DomainLifetimeId == 0) throw new ArgumentOutOfRangeException("GUI owner identity");
                this.World = World ?? throw new ArgumentNullException("World"); Limits = World.Limits;
                this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId;
                World.RegisterRegistry(this);
            }
            internal int LiveObjectCount { get { return State.Nodes.Count; } }
            internal int ScreenCount { get { return State.Screens; } }
            internal int ConnectionCount { get { return State.Connections.Count; } }
            internal int PresentationCount { get { return State.Presentations.Count; } }
            internal int PublicationDepth { get { return Publications.Count; } }
            internal int PendingDestroyCount { get { return State.PendingDestroys.Count; } }
            internal int DirtyPresentationCount {
                get {
                    int Result = 0;
                    foreach (GuiPresentation Value in State.Presentations.Values) {
                        GuiScreenSynchronization Synchronization;
                        if (State.Synchronization.TryGetValue(Value.ScreenId, out Synchronization) && Value.SentRevision < Synchronization.Revision) Result++;
                    }
                    return Result;
                }
            }
            internal int FullResyncPresentationCount {
                get {
                    int Result = 0;
                    foreach (GuiPresentation Value in State.Presentations.Values) {
                        GuiScreenSynchronization Synchronization;
                        if (Value.NeedsFullResync || (State.Synchronization.TryGetValue(Value.ScreenId, out Synchronization) && Synchronization.FullRebuildRequired)) Result++;
                    }
                    return Result;
                }
            }
            internal int BlockedPresentationCount {
                get {
                    int Result = 0;
                    foreach (GuiPresentation Value in State.Presentations.Values) {
                        GuiScreenSynchronization Synchronization;
                        if (Value.ProjectionBlockedRevision != 0 && State.Synchronization.TryGetValue(Value.ScreenId, out Synchronization) &&
                            Value.ProjectionBlockedRevision == Synchronization.Revision) Result++;
                    }
                    return Result;
                }
            }
            internal bool HasWork {
                get {
                    if (State.PendingDestroys.Count != 0) return true;
                    foreach (GuiPresentation Value in State.Presentations.Values) if (PresentationNeedsWork(Value)) return true;
                    return false;
                }
            }

            public bool IsDomainLive(ulong Vm, ulong Domain) { return !Disposed && Vm == VmGenerationId && Domain == DomainLifetimeId; }
            public bool TryGetClass(GuiObjectIdentity Identity, out GuiClassId ClassId)
            {
                GuiRetainedNode Node;
                if (IsDomainLive(Identity.VmGenerationId, Identity.DomainLifetimeId) && State.Nodes.TryGetValue(Identity.GuiObjectId, out Node)) {
                    ClassId = Node.ClassId; return true;
                }
                ClassId = 0; return false;
            }

            internal void BeginPublication()
            {
                RequireLive(); if (Publications.Count >= 64) {
                    World.RuntimeDiagnostics.ResourceLimitRejected(); throw new FacadeException("GUI publication nesting limit reached");
                }
                Publications.Push(State.Copy());
            }
            internal void CommitPublication()
            {
                RequireLive(); if (Publications.Count == 0) throw new FacadeException("invalid GUI publication commit"); Publications.Pop();
                if (Publications.Count == 0) ReconcileCommittedActions();
            }
            internal void RollbackPublication()
            {
                RequireLive(); if (Publications.Count == 0) throw new FacadeException("invalid GUI publication rollback");
                GuiRetainedState Previous = Publications.Pop(); World.Restore(Previous.Nodes.Count - State.Nodes.Count);
                World.RestorePresentations(State.Presentations.Values, Previous.Presentations.Values); State = Previous;
            }

            internal string[] Query(string[] Fields)
            {
                RequireLive(); if (Fields == null || Fields.Length == 0) throw new FacadeException("invalid GUI query");
                switch (Fields[0]) {
                    case "get": RequireFields(Fields, 3); return ReadProperty(Node(Id(Fields[1])), Fields[2]);
                    case "children": {
                        RequireFields(Fields, 2); GuiRetainedNode Parent = Node(Id(Fields[1])); var Result = new List<string>();
                        foreach (ulong ChildId in Parent.Children) { GuiRetainedNode Child = Node(ChildId); Result.Add(ChildId.ToString(CultureInfo.InvariantCulture)); Result.Add(GuiSchema.GetClass(Child.ClassId).Name); }
                        return Result.ToArray();
                    }
                    case "find": {
                        RequireFields(Fields, 3); ValidateText(Fields[2], Limits.MaxNameUtf8Bytes, "GUI name"); GuiRetainedNode Parent = Node(Id(Fields[1]));
                        foreach (ulong ChildId in Parent.Children) { GuiRetainedNode Child = Node(ChildId); if (Child.Properties[GuiPropertyId.Name].Text == Fields[2]) return ObjectResult(Child); }
                        return new string[0];
                    }
                    case "isa": {
                        RequireFields(Fields, 3); GuiRetainedNode Value = Node(Id(Fields[1])); GuiClassDescriptor Ignored;
                        bool Known = GuiSchema.TryGetClass(Fields[2], out Ignored); return new[] {Known && GuiSchema.IsA(Value.ClassId, Fields[2]) ? "1" : "0"};
                    }
                    case "event": {
                        RequireFields(Fields, 3); GuiRetainedNode Value = Node(Id(Fields[1]));
                        if (Fields[2] != "Activated" || !IsActivatedClass(Value.ClassId)) throw new FacadeException("event is not available on GUI object");
                        return new string[0];
                    }
                    case "shown": {
                        RequireFields(Fields, 4); GuiRetainedNode Screen = Node(Id(Fields[1])); RequireScreen(Screen);
                        if (World.Resolve(Fields[2], Fields[3]) == null) throw new FacadeException("Player is no longer connected");
                        return new[] {State.Presentations.ContainsKey(PresentationKey(Screen.Identity.GuiObjectId, Fields[2])) ? "1" : "0"};
                    }
                    default: throw new FacadeException("unknown GUI query");
                }
            }

            internal string[] Mutate(string[] Fields, Func<string> NextRegistration)
            {
                RequireLive(); if (Fields == null || Fields.Length == 0) throw new FacadeException("invalid GUI mutation");
                try { switch (Fields[0]) {
                    case "create": {
                        RequireFields(Fields, 3); ulong ParentId = Fields[1].Length == 0 ? 0 : Id(Fields[1]);
                        return ObjectResult(Create(ParentId, Fields[2]));
                    }
                    case "set": if (Fields.Length < 4) throw new FacadeException("invalid GUI property assignment"); SetProperty(Node(Id(Fields[1])), Fields[2], Fields, 3); return new string[0];
                    case "clone": RequireFields(Fields, 2); return ObjectResult(Clone(Node(Id(Fields[1]))));
                    case "destroy": RequireFields(Fields, 2); return Destroy(Id(Fields[1]));
                    case "connect": {
                        RequireFields(Fields, 2); GuiRetainedNode Button = Node(Id(Fields[1]));
                        if (!IsActivatedClass(Button.ClassId)) throw new FacadeException("Activated is only available on button GUI objects");
                        int Count = 0; foreach (ulong Owner in State.Connections.Values) if (Owner == Button.Identity.GuiObjectId) Count++;
                        if (Count >= Limits.MaxSignalConnectionsPerButton || State.Connections.Count >= Limits.MaxGuiSignalConnectionsPerDomain)
                            throw new FacadeException("GUI Signal connection limit reached");
                        string Registration = NextRegistration(); State.Connections.Add(Registration, Button.Identity.GuiObjectId);
                        MarkStructural(RootScreen(Button)); return new[] {Registration};
                    }
                    case "disconnect": {
                        RequireFields(Fields, 3); ulong ObjectId = Id(Fields[1]); ulong Owner;
                        if (State.Connections.TryGetValue(Fields[2], out Owner) && Owner == ObjectId) {
                            GuiRetainedNode Button = Node(ObjectId); State.Connections.Remove(Fields[2]); MarkStructural(RootScreen(Button));
                        }
                        return new string[0];
                    }
                    case "show": RequireFields(Fields, 4); Show(Node(Id(Fields[1])), Fields[2], Fields[3]); return new string[0];
                    case "hide": RequireFields(Fields, 4); Hide(Node(Id(Fields[1])), Fields[2], Fields[3]); return new string[0];
                    default: throw new FacadeException("unknown GUI mutation");
                } } catch (FacadeException Error) { World.RecordResourceLimit(Error); throw; }
            }

            private GuiRetainedNode Create(ulong ParentId, string ClassName)
            {
                GuiClassDescriptor Descriptor;
                if (!GuiSchema.TryGetClass(ClassName, out Descriptor) || !Descriptor.Public || !Descriptor.Constructible)
                    throw new FacadeException("unknown or nonconstructible GUI class");
                GuiCreationScope Scope = ParentId == 0 ? GuiCreationScope.GuiService : GuiCreationScope.GuiObject;
                if ((Descriptor.CreationScope & Scope) == 0) throw new FacadeException("GUI class cannot be created in this scope");
                if (Descriptor.Id == GuiClassId.ScreenGui && ParentId != 0) throw new FacadeException("ScreenGui must be root-level");
                if (State.Nodes.Count >= Limits.MaxObjectsPerDomain) throw new FacadeException("domain GUI object limit reached");
                if (Descriptor.Id == GuiClassId.ScreenGui && State.Screens >= Limits.MaxScreensPerDomain) throw new FacadeException("domain ScreenGui limit reached");
                GuiRetainedNode Parent = ParentId == 0 ? null : Node(ParentId);
                if (Parent != null) ValidateAttachment(null, Descriptor.Id, Parent, 1, 1, IsActivatedClass(Descriptor.Id) ? 1 : 0,
                    TextBytes(Descriptor.Id), ProjectionCost(Descriptor.Id));
                if (NextObjectId == ulong.MaxValue) throw new FacadeException("GUI object identity exhausted");
                World.Adjust(1); ulong ObjectId = NextObjectId++;
                var Result = new GuiRetainedNode(new GuiObjectIdentity(VmGenerationId, DomainLifetimeId, ObjectId), Descriptor.Id);
                ApplyDefaults(Result); State.Nodes.Add(ObjectId, Result); if (Descriptor.Id == GuiClassId.ScreenGui) State.Screens++;
                if (Descriptor.Id == GuiClassId.ScreenGui) State.Synchronization.Add(ObjectId, new GuiScreenSynchronization());
                if (Parent != null) { Attach(Result, Parent); MarkStructural(RootScreen(Parent)); }
                return Result;
            }

            private GuiRetainedNode Clone(GuiRetainedNode Source)
            {
                var SourceIds = new List<ulong>(); Collect(Source, SourceIds);
                int Depth = SubtreeDepth(Source); if (SourceIds.Count > Limits.MaxCloneObjects || Depth > Limits.MaxCloneDepth)
                    throw new FacadeException("GUI clone limit reached");
                if (State.Nodes.Count + SourceIds.Count > Limits.MaxObjectsPerDomain) throw new FacadeException("domain GUI object limit reached");
                int ScreenCopies = Source.ClassId == GuiClassId.ScreenGui ? 1 : 0;
                if (State.Screens + ScreenCopies > Limits.MaxScreensPerDomain) throw new FacadeException("domain ScreenGui limit reached");
                if (NextObjectId > ulong.MaxValue - (ulong)SourceIds.Count) throw new FacadeException("GUI object identity exhausted");
                World.Adjust(SourceIds.Count);
                var Map = new Dictionary<ulong, GuiRetainedNode>();
                foreach (ulong OldId in SourceIds) {
                    GuiRetainedNode Old = State.Nodes[OldId]; ulong NewId = NextObjectId++;
                    var Copy = new GuiRetainedNode(new GuiObjectIdentity(VmGenerationId, DomainLifetimeId, NewId), Old.ClassId);
                    foreach (var Property in Old.Properties) Copy.Properties.Add(Property.Key, Property.Value);
                    State.Nodes.Add(NewId, Copy); Map.Add(OldId, Copy);
                }
                foreach (ulong OldId in SourceIds) foreach (ulong OldChild in State.Nodes[OldId].Children) {
                    GuiRetainedNode Parent = Map[OldId], Child = Map[OldChild]; Parent.Children.Add(Child.Identity.GuiObjectId); Child.ParentId = Parent.Identity.GuiObjectId;
                }
                State.Screens += ScreenCopies;
                if (ScreenCopies != 0) State.Synchronization.Add(Map[Source.Identity.GuiObjectId].Identity.GuiObjectId, new GuiScreenSynchronization());
                return Map[Source.Identity.GuiObjectId];
            }

            private string[] Destroy(ulong ObjectId)
            {
                GuiRetainedNode Root;
                if (!State.Nodes.TryGetValue(ObjectId, out Root)) {
                    if (ObjectId > 0 && ObjectId < NextObjectId) return new string[0];
                    throw new FacadeException("unknown GUI object identity");
                }
                var RemovedIds = new List<ulong>(); Collect(Root, RemovedIds); var RemovedConnections = new List<string>();
                GuiRetainedNode AffectedScreen = Root.ClassId == GuiClassId.ScreenGui ? Root : RootScreen(Root);
                if (Root.ClassId == GuiClassId.ScreenGui) RemovePresentationsForScreen(Root.Identity.GuiObjectId);
                if (Root.ParentId.HasValue) State.Nodes[Root.ParentId.Value].Children.Remove(ObjectId);
                foreach (ulong RemovedId in RemovedIds) {
                    GuiRetainedNode Removed = State.Nodes[RemovedId];
                    if (Removed.ClassId == GuiClassId.ScreenGui) State.Screens--;
                    foreach (var Connection in new List<KeyValuePair<string, ulong>>(State.Connections)) if (Connection.Value == RemovedId) {
                        RemovedConnections.Add(Connection.Key); State.Connections.Remove(Connection.Key);
                    }
                    State.Nodes.Remove(RemovedId);
                }
                if (Root.ClassId == GuiClassId.ScreenGui) State.Synchronization.Remove(Root.Identity.GuiObjectId);
                World.Adjust(-RemovedIds.Count); if (AffectedScreen != null && Root.ClassId != GuiClassId.ScreenGui)
                    MarkStructural(AffectedScreen, GuiActionRejection.TargetUnavailable);
                return RemovedConnections.ToArray();
            }

            private void SetProperty(GuiRetainedNode Node, string Name, string[] Fields, int KindIndex)
            {
                GuiPropertyUse Use;
                if (!GuiSchema.TryGetProperty(Node.ClassId, Name, out Use) || !Use.Writable) throw new FacadeException("GUI property is unknown or read-only");
                if (Use.Descriptor.Id == GuiPropertyId.Parent) { SetParent(Node, Fields, KindIndex); return; }
                GuiStoredValue Value = ParseValue(Use.Descriptor, Fields, KindIndex);
                GuiStoredValue Existing = Node.Properties[Use.Descriptor.Id]; if (Existing.SameAs(Value)) return;
                if (Use.Descriptor.Id == GuiPropertyId.Text) {
                    GuiRetainedNode Screen = RootScreen(Node); if (Screen != null) {
                        int Current = ScreenTextBytes(Screen); int Old = FacadePolicy.Utf8.GetByteCount(Node.Properties[GuiPropertyId.Text].Text);
                        int Next = FacadePolicy.Utf8.GetByteCount(Value.Text);
                        if (Current - Old + Next > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
                    }
                }
                Node.Properties[Use.Descriptor.Id] = Value;
                GuiRetainedNode ScreenValue = RootScreen(Node);
                if (Use.Descriptor.Id == GuiPropertyId.Visible && ContainsConnectedButton(Node))
                    MarkStructural(ScreenValue, GuiActionRejection.TargetUnavailable);
                else if (Use.Descriptor.MutationKind == GuiMutationKind.Structural) MarkStructural(ScreenValue);
                else if (Use.Descriptor.MutationKind == GuiMutationKind.Patchable) {
                    MarkPatchable(ScreenValue, Node.Identity.GuiObjectId, Use.Descriptor.Id);
                    if ((Use.Descriptor.Id == GuiPropertyId.Size || Use.Descriptor.Id == GuiPropertyId.Visible) && HasListParent(Node))
                        MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                } else if (Use.Descriptor.MutationKind == GuiMutationKind.LayoutAffecting) {
                    if (Node.ClassId == GuiClassId.UIListLayout || Node.ClassId == GuiClassId.UIPadding) {
                        if (Node.ParentId.HasValue) MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                    } else if (Use.Descriptor.Id == GuiPropertyId.LayoutOrder && HasListParent(Node))
                        MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                }
            }

            private void SetParent(GuiRetainedNode Child, string[] Fields, int KindIndex)
            {
                if (Child.ClassId == GuiClassId.ScreenGui) throw new FacadeException("ScreenGui.Parent is read-only");
                if (KindIndex >= Fields.Length) throw new FacadeException("invalid Parent value");
                GuiRetainedNode Parent = null;
                if (Fields[KindIndex] == "object") {
                    if (Fields.Length != KindIndex + 2) throw new FacadeException("invalid Parent value"); Parent = Node(Id(Fields[KindIndex + 1]));
                } else if (Fields[KindIndex] == "nil") {
                    if (Fields.Length != KindIndex + 1) throw new FacadeException("invalid Parent value");
                } else throw new FacadeException("Parent expects a same-domain GUI object or nil");
                if (Parent != null && Child.ParentId == Parent.Identity.GuiObjectId) return;
                int Objects = SubtreeCount(Child), Depth = SubtreeDepth(Child), Buttons = SubtreeButtons(Child), TextBytes = SubtreeTextBytes(Child);
                if (Parent != null) ValidateAttachment(Child, Child.ClassId, Parent, Objects, Depth, Buttons, TextBytes, SubtreeProjectionCost(Child));
                GuiRetainedNode OldScreen = RootScreen(Child), NewScreen = Parent == null ? null : RootScreen(Parent);
                if (Child.ParentId.HasValue) State.Nodes[Child.ParentId.Value].Children.Remove(Child.Identity.GuiObjectId);
                Child.ParentId = null; if (Parent != null) Attach(Child, Parent);
                MarkStructural(OldScreen, GuiActionRejection.TargetUnavailable);
                if (NewScreen != OldScreen) MarkStructural(NewScreen, GuiActionRejection.TargetUnavailable);
            }

            private void Show(GuiRetainedNode Screen, string PlayerToken, string PlayerUserId)
            {
                RequireScreen(Screen); if (World.Resolve(PlayerToken, PlayerUserId) == null) throw new FacadeException("Player is no longer connected");
                string Key = PresentationKey(Screen.Identity.GuiObjectId, PlayerToken); GuiPresentation Existing;
                if (State.Presentations.TryGetValue(Key, out Existing)) {
                    if (Existing.SynchronizationUncertain) { RequireFullRebuild(Existing); Existing.ProjectionBlockedRevision = 0; }
                    return;
                }
                int Viewers = 0; foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == Screen.Identity.GuiObjectId) Viewers++;
                if (Viewers >= Limits.MaxViewersPerScreen) throw new FacadeException("ScreenGui viewer limit reached");
                if (State.Presentations.Count >= Limits.MaxPresentationsPerDomain) throw new FacadeException("domain GUI presentation limit reached");
                if (NextPresentationEpoch == ulong.MaxValue) throw new FacadeException("GUI presentation epoch exhausted");
                var Presentation = new GuiPresentation(Screen.Identity.GuiObjectId, NextPresentationEpoch++, PlayerToken, PlayerUserId, World.NewClientPrefix());
                GuiRenderCompiler.Compile(State, Screen, Presentation, Limits);
                World.AddPresentation(PlayerToken);
                try { State.Presentations.Add(Key, Presentation); }
                catch { World.RemovePresentation(PlayerToken); throw; }
            }
            private void Hide(GuiRetainedNode Screen, string PlayerToken, string PlayerUserId)
            {
                RequireScreen(Screen); if (World.Resolve(PlayerToken, PlayerUserId) == null) throw new FacadeException("Player is no longer connected");
                string Key = PresentationKey(Screen.Identity.GuiObjectId, PlayerToken); GuiPresentation Presentation;
                if (!State.Presentations.TryGetValue(Key, out Presentation)) return;
                InvalidatePresentation(Presentation, GuiActionRejection.TargetUnavailable);
                State.Presentations.Remove(Key); World.RemovePresentation(Presentation.PlayerToken);
                if (Presentation.WasSent) State.PendingDestroys.Enqueue(Presentation.Target);
                PruneSynchronization(Screen.Identity.GuiObjectId);
            }
            internal void Disconnect(PlayerLifetime Player)
            {
                RequireLive(); if (Player == null) return;
                var Keys = new List<string>(); foreach (var Value in State.Presentations) if (Value.Value.PlayerToken == Player.Token) Keys.Add(Value.Key);
                var Screens = new HashSet<ulong>();
                foreach (string Key in Keys) {
                    GuiPresentation Value = State.Presentations[Key]; Screens.Add(Value.ScreenId);
                    InvalidatePresentation(Value, GuiActionRejection.Stale); World.RemovePresentation(Value.PlayerToken); State.Presentations.Remove(Key);
                }
                if (State.PendingDestroys.Count != 0) {
                    var Keep = new Queue<GuiBackendTarget>(); while (State.PendingDestroys.Count != 0) {
                        GuiBackendTarget Target = State.PendingDestroys.Dequeue(); if (Target.ExactPlayerConnectionToken != Player.Token) Keep.Enqueue(Target);
                    }
                    while (Keep.Count != 0) State.PendingDestroys.Enqueue(Keep.Dequeue());
                }
                foreach (ulong ScreenId in Screens) PruneSynchronization(ScreenId);
            }
            internal int FlushOne(int RemainingBytes)
            { return FlushOne(RemainingBytes, 0, false); }
            internal int FlushOne(int RemainingBytes, ulong FlushCycle)
            { return FlushOne(RemainingBytes, FlushCycle, true); }
            private int FlushOne(int RemainingBytes, ulong FlushCycle, bool TrackWorldCycle)
            {
                RequireLive(); if (Publications.Count != 0) return 0;
                string SelectedKey; GuiPresentation Selected; bool HasPresentation = SelectPresentation(FlushCycle, TrackWorldCycle, out SelectedKey, out Selected);
                if (State.PendingDestroys.Count != 0 && (PreferDestroy || !HasPresentation)) {
                    GuiBackendTarget Target = State.PendingDestroys.Peek(); int DestroyBytes = World.Backend.MeasureDestroy(Target);
                    if (DestroyBytes > RemainingBytes) return -DestroyBytes;
                    State.PendingDestroys.Dequeue(); GuiBackendResult DestroyResult;
                    try { DestroyResult = World.Backend.Destroy(Target); }
                    catch { DestroyResult = GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "GUI backend destroy failed"); }
                    if (!DestroyResult.Accepted) World.BackendFailed();
                    PreferDestroy = false; return DestroyBytes;
                }
                if (!HasPresentation) return Int32.MinValue;
                if (TrackWorldCycle) Selected.LastAttemptCycle = FlushCycle;
                PresentationFlushCursor = SelectedKey; PreferDestroy = true;
                if (World.Resolve(Selected.PlayerToken, Selected.PlayerUserId) == null) {
                    InvalidatePresentation(Selected, GuiActionRejection.Stale); State.Presentations.Remove(SelectedKey);
                    World.RemovePresentation(Selected.PlayerToken); PruneSynchronization(Selected.ScreenId); return 0;
                }
                GuiRetainedNode Screen;
                if (!State.Nodes.TryGetValue(Selected.ScreenId, out Screen)) {
                    InvalidatePresentation(Selected, GuiActionRejection.TargetUnavailable); State.Presentations.Remove(SelectedKey);
                    World.RemovePresentation(Selected.PlayerToken); State.Synchronization.Remove(Selected.ScreenId); return 0;
                }
                GuiScreenSynchronization Synchronization = State.Synchronization[Selected.ScreenId]; ulong Revision = Synchronization.Revision;
                bool Full = Selected.NeedsFullResync || !Selected.WasSent || Synchronization.FullRebuildRequired ||
                    Selected.SuccessfulPatchBatches >= Limits.PatchBatchesBeforeFull;
                GuiRenderPlan Plan = null; GuiRenderPatch Patch = null; int Bytes;
                if (!Full) {
                    try { Patch = GuiRenderCompiler.CompilePatch(State, Screen, Selected, Synchronization, Limits); }
                    catch (GuiFullRebuildRequiredException) { Full = true; }
                    catch (InvalidOperationException) { Full = true; }
                }
                if (Full) RequireFullRebuild(Selected);
                var CandidateActions = new List<GuiActionRecord>();
                var CandidateTokens = new Dictionary<ulong, string>();
                var PendingTokens = new HashSet<string>(StringComparer.Ordinal);
                if (Full) {
                    try {
                        PlayerLifetime ExactPlayer = World.ExactPlayer(Selected.PlayerToken, Selected.PlayerUserId);
                        if (ExactPlayer == null) throw new InvalidOperationException("GUI Player lifetime is stale");
                        Plan = GuiRenderCompiler.Compile(State, Screen, Selected, Limits, Button => {
                            if (!HasConnection(Button.Identity.GuiObjectId)) return null;
                            string Token = World.NewActionToken(PendingTokens); PendingTokens.Add(Token);
                            var Record = new GuiActionRecord(Token, this, Selected, Button, ExactPlayer, Limits, Stopwatch.GetTimestamp());
                            CandidateActions.Add(Record); CandidateTokens.Add(Button.Identity.GuiObjectId, Token);
                            return GuiRetainedWorld.ActionCommand + " " + Token;
                        });
                        World.EnsureActionCapacity(this, CandidateActions.Count);
                        Bytes = World.Backend.MeasureReplace(Selected.Target, Plan);
                    }
                    catch (Exception Error) {
                        World.RecordResourceLimit(Error);
                        Selected.NeedsFullResync = false; Selected.SynchronizationUncertain = true;
                        Selected.ProjectionBlockedRevision = Revision; return 0;
                    }
                } else {
                    try { Bytes = World.Backend.MeasureUpdate(Selected.Target, Patch); }
                    catch {
                        RequireFullRebuild(Selected); Selected.SuccessfulPatchBatches = 0;
                        return 0;
                    }
                }
                if (Bytes > RemainingBytes) return -Bytes;
                GuiBackendResult Result;
                try { Result = Full ? World.Backend.Replace(Selected.Target, Plan) : World.Backend.Update(Selected.Target, Patch); }
                catch { Result = GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "GUI backend send failed"); }
                if (Result.Accepted) {
                    World.BackendAccepted(Full);
                    if (Full) {
                        try { World.ActivateActions(this, CandidateActions); }
                        catch (Exception Error) {
                            World.RecordResourceLimit(Error);
                            AdvancePresentationEpoch(Selected); Selected.NeedsFullResync = true; Selected.SynchronizationUncertain = true;
                            Selected.SuccessfulPatchBatches = 0; return Bytes;
                        }
                        Selected.ActionTokens.Clear(); foreach (var Value in CandidateTokens) Selected.ActionTokens.Add(Value.Key, Value.Value);
                    }
                    Selected.WasSent = true; Selected.SentRevision = Revision; Selected.NeedsFullResync = false;
                    Selected.SynchronizationUncertain = false; Selected.ProjectionBlockedRevision = 0;
                    Selected.LastNeedsCursor = GuiRenderCompiler.NeedsCursorFor(State, Screen);
                    Selected.SuccessfulPatchBatches = Full ? 0 : Selected.SuccessfulPatchBatches + 1;
                    PruneSynchronization(Selected.ScreenId);
                } else if (!Full || Result.Code != GuiBackendResultCode.TargetUnavailable || World.Resolve(Selected.PlayerToken, Selected.PlayerUserId) != null) {
                    World.BackendFailed();
                    if (Full) AdvancePresentationEpoch(Selected);
                    RequireFullRebuild(Selected); Selected.SynchronizationUncertain = true; Selected.SuccessfulPatchBatches = 0;
                } else {
                    World.BackendFailed();
                    InvalidatePresentation(Selected, GuiActionRejection.Stale); State.Presentations.Remove(SelectedKey);
                    World.RemovePresentation(Selected.PlayerToken); PruneSynchronization(Selected.ScreenId);
                }
                return Bytes;
            }
            private void RemovePresentationsForScreen(ulong ScreenId)
            {
                var Keys = new List<string>(); foreach (var Value in State.Presentations) if (Value.Value.ScreenId == ScreenId) Keys.Add(Value.Key);
                foreach (string Key in Keys) {
                    GuiPresentation Presentation = State.Presentations[Key]; State.Presentations.Remove(Key); World.RemovePresentation(Presentation.PlayerToken);
                    InvalidatePresentation(Presentation, GuiActionRejection.TargetUnavailable);
                    if (Presentation.WasSent) State.PendingDestroys.Enqueue(Presentation.Target);
                }
                PruneSynchronization(ScreenId);
            }
            private bool SelectPresentation(ulong FlushCycle, bool TrackWorldCycle, out string SelectedKey, out GuiPresentation Selected)
            {
                SelectedKey = null; Selected = null;
                foreach (var Value in State.Presentations) if ((!TrackWorldCycle || Value.Value.LastAttemptCycle != FlushCycle) && PresentationNeedsWork(Value.Value) &&
                    StringComparer.Ordinal.Compare(Value.Key, PresentationFlushCursor ?? "") > 0 &&
                    (SelectedKey == null || StringComparer.Ordinal.Compare(Value.Key, SelectedKey) < 0)) { SelectedKey = Value.Key; Selected = Value.Value; }
                if (Selected != null) return true;
                foreach (var Value in State.Presentations) if ((!TrackWorldCycle || Value.Value.LastAttemptCycle != FlushCycle) && PresentationNeedsWork(Value.Value) &&
                    (SelectedKey == null || StringComparer.Ordinal.Compare(Value.Key, SelectedKey) < 0)) { SelectedKey = Value.Key; Selected = Value.Value; }
                return Selected != null;
            }
            private bool PresentationNeedsWork(GuiPresentation Presentation)
            {
                GuiScreenSynchronization Synchronization;
                if (!State.Synchronization.TryGetValue(Presentation.ScreenId, out Synchronization) ||
                    Presentation.ProjectionBlockedRevision == Synchronization.Revision) return false;
                return Presentation.NeedsFullResync || Presentation.SentRevision < Synchronization.Revision;
            }
            private void MarkStructural(GuiRetainedNode Screen, GuiActionRejection ActionReason = GuiActionRejection.Stale)
            {
                if (Screen == null) return; GuiScreenSynchronization Synchronization = Advance(Screen.Identity.GuiObjectId);
                Synchronization.DirtyObjects.Clear(); Synchronization.FullRebuildRequired = HasPresentation(Screen.Identity.GuiObjectId);
                foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == Screen.Identity.GuiObjectId) {
                    RequireFullRebuild(Value, ActionReason); Value.ProjectionBlockedRevision = 0; Value.SuccessfulPatchBatches = 0;
                }
            }
            private void MarkPatchable(GuiRetainedNode Screen, ulong ObjectId, GuiPropertyId Property)
            {
                if (Screen == null) return; GuiScreenSynchronization Synchronization = Advance(Screen.Identity.GuiObjectId);
                if (!HasPresentation(Screen.Identity.GuiObjectId)) { Synchronization.DirtyObjects.Clear(); Synchronization.FullRebuildRequired = false; return; }
                if (Synchronization.FullRebuildRequired) return;
                AddDirty(Screen, Synchronization, ObjectId, Property);
            }
            private void MarkLayoutAffected(GuiRetainedNode Parent)
            {
                if (Parent == null) return;
                GuiRetainedNode Screen = RootScreen(Parent); if (Screen == null) return;
                GuiScreenSynchronization Synchronization = Advance(Screen.Identity.GuiObjectId);
                if (!HasPresentation(Screen.Identity.GuiObjectId)) { Synchronization.DirtyObjects.Clear(); Synchronization.FullRebuildRequired = false; return; }
                if (Synchronization.FullRebuildRequired) return;
                foreach (ulong ChildId in Parent.Children) {
                    GuiRetainedNode Child = State.Nodes[ChildId];
                    if (GuiSchema.IsA(Child.ClassId, "GuiObject") && !AddDirty(Screen, Synchronization, ChildId, GuiPropertyId.LayoutProjection)) return;
                }
                if (Parent.ClassId == GuiClassId.TextLabel || Parent.ClassId == GuiClassId.TextButton)
                    AddDirty(Screen, Synchronization, Parent.Identity.GuiObjectId, GuiPropertyId.ContentProjection);
            }
            private bool AddDirty(GuiRetainedNode Screen, GuiScreenSynchronization Synchronization, ulong ObjectId, GuiPropertyId Property)
            {
                HashSet<GuiPropertyId> Properties;
                if (!Synchronization.DirtyObjects.TryGetValue(ObjectId, out Properties)) {
                    if (TrackedDirtyObjects() >= Limits.MaxTrackedDirtyObjectsPerDomain) {
                        Synchronization.DirtyObjects.Clear(); Synchronization.FullRebuildRequired = true;
                        foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == Screen.Identity.GuiObjectId) RequireFullRebuild(Value);
                        return false;
                    }
                    Properties = new HashSet<GuiPropertyId>(); Synchronization.DirtyObjects.Add(ObjectId, Properties);
                }
                Properties.Add(Property);
                foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == Screen.Identity.GuiObjectId) Value.ProjectionBlockedRevision = 0;
                return true;
            }
            private GuiScreenSynchronization Advance(ulong ScreenId)
            {
                GuiScreenSynchronization Result = State.Synchronization[ScreenId];
                if (Result.Revision == ulong.MaxValue) throw new FacadeException("ScreenGui revision exhausted");
                Result.Revision++; return Result;
            }
            private int TrackedDirtyObjects()
            { int Result = 0; foreach (GuiScreenSynchronization Value in State.Synchronization.Values) Result += Value.DirtyObjects.Count; return Result; }
            private bool HasPresentation(ulong ScreenId)
            { foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == ScreenId) return true; return false; }
            private void PruneSynchronization(ulong ScreenId)
            {
                GuiScreenSynchronization Synchronization; if (!State.Synchronization.TryGetValue(ScreenId, out Synchronization)) return;
                foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == ScreenId && Value.SentRevision < Synchronization.Revision) return;
                Synchronization.DirtyObjects.Clear(); Synchronization.FullRebuildRequired = false;
            }

            private bool HasConnection(ulong ButtonId)
            { foreach (ulong Value in State.Connections.Values) if (Value == ButtonId) return true; return false; }
            private bool ContainsConnectedButton(GuiRetainedNode Root)
            {
                if (IsActivatedClass(Root.ClassId) && HasConnection(Root.Identity.GuiObjectId)) return true;
                foreach (ulong Child in Root.Children) if (ContainsConnectedButton(State.Nodes[Child])) return true;
                return false;
            }
            private bool EffectivelyVisible(GuiRetainedNode Button, GuiRetainedNode Screen)
            {
                for (GuiRetainedNode Value = Button; Value != null && Value != Screen;
                    Value = Value.ParentId.HasValue ? State.Nodes[Value.ParentId.Value] : null) {
                    GuiStoredValue Visible;
                    if (!Value.Properties.TryGetValue(GuiPropertyId.Visible, out Visible) || !Visible.Boolean) return false;
                    if (!Value.ParentId.HasValue) return false;
                }
                return RootScreen(Button) == Screen;
            }
            private void RequireFullRebuild(GuiPresentation Presentation, GuiActionRejection Reason = GuiActionRejection.Stale)
            {
                bool Rotate = Presentation.WasSent && !Presentation.NeedsFullResync;
                if (Presentation.ActionTokens.Count != 0 || Presentation.ActionInvalidationPending) {
                    InvalidatePresentation(Presentation, Reason); Rotate = true;
                }
                if (Rotate) AdvancePresentationEpoch(Presentation);
                Presentation.NeedsFullResync = true;
            }
            private void AdvancePresentationEpoch(GuiPresentation Presentation)
            {
                if (NextPresentationEpoch == ulong.MaxValue) throw new FacadeException("GUI presentation epoch exhausted");
                Presentation.Epoch = NextPresentationEpoch++;
            }
            private void InvalidatePresentation(GuiPresentation Presentation, GuiActionRejection Reason)
            {
                if (Publications.Count != 0) { Presentation.ActionInvalidationPending = true; return; }
                foreach (string Token in Presentation.ActionTokens.Values) World.InvalidateAction(Token, Reason);
                Presentation.ActionTokens.Clear(); Presentation.ActionInvalidationPending = false;
            }
            private void ReconcileCommittedActions()
            {
                var Keep = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiPresentation Presentation in State.Presentations.Values) {
                    if (Presentation.ActionInvalidationPending) {
                        foreach (string Token in Presentation.ActionTokens.Values) World.InvalidateAction(Token, GuiActionRejection.Stale);
                        Presentation.ActionTokens.Clear(); Presentation.ActionInvalidationPending = false;
                    }
                    foreach (string Token in Presentation.ActionTokens.Values) Keep.Add(Token);
                }
                World.ReconcileActions(this, Keep, GuiActionRejection.Stale);
            }

            internal GuiActionRejection ValidateAction(GuiActionRecord Record, string Registration, out string[] Registrations)
            {
                Registrations = new string[0];
                if (Disposed || Record == null || Record.Registry != this || Record.VmGenerationId != VmGenerationId ||
                    Record.DomainLifetimeId != DomainLifetimeId) return GuiActionRejection.Stale;
                GuiPresentation Presentation;
                if (!State.Presentations.TryGetValue(PresentationKey(Record.ScreenId, Record.PlayerToken), out Presentation) ||
                    Presentation.Epoch != Record.PresentationEpoch || Presentation.ActionInvalidationPending) return GuiActionRejection.Stale;
                string ActiveToken;
                if (!Presentation.ActionTokens.TryGetValue(Record.ButtonId, out ActiveToken) || ActiveToken != Record.Token) return GuiActionRejection.Stale;
                PlayerLifetime Player = World.ExactPlayer(Record.PlayerToken, Record.PlayerUserId);
                if (Player == null || !Object.ReferenceEquals(Player.Identity, Record.PlayerIdentity) ||
                    !Object.ReferenceEquals(Player.Connection, Record.PlayerConnection)) return GuiActionRejection.Stale;
                GuiRetainedNode Screen, Button;
                if (!State.Nodes.TryGetValue(Record.ScreenId, out Screen) || Screen.ClassId != GuiClassId.ScreenGui ||
                    !State.Nodes.TryGetValue(Record.ButtonId, out Button) || !IsActivatedClass(Button.ClassId) ||
                    !EffectivelyVisible(Button, Screen)) return GuiActionRejection.TargetUnavailable;
                var Values = new List<KeyValuePair<ulong, string>>();
                foreach (var Value in State.Connections) if (Value.Value == Record.ButtonId) {
                    ulong Numeric; if (UInt64.TryParse(Value.Key, NumberStyles.None, CultureInfo.InvariantCulture, out Numeric))
                        Values.Add(new KeyValuePair<ulong, string>(Numeric, Value.Key));
                }
                Values.Sort((Left, Right) => Left.Key.CompareTo(Right.Key));
                if (Values.Count == 0 || (Registration != null && !Values.Exists(Value => Value.Value == Registration)))
                    return GuiActionRejection.TargetUnavailable;
                Registrations = Values.ConvertAll(Value => Value.Value).ToArray(); return GuiActionRejection.None;
            }
            private static void RequireScreen(GuiRetainedNode Screen)
            { if (Screen.ClassId != GuiClassId.ScreenGui) throw new FacadeException("Show, Hide and IsShown require ScreenGui"); }
            private static string PresentationKey(ulong ScreenId, string PlayerToken)
            { return ScreenId.ToString(CultureInfo.InvariantCulture) + ":" + PlayerToken; }

            private void ValidateAttachment(GuiRetainedNode Child, GuiClassId ChildClass, GuiRetainedNode Parent, int Objects, int Depth,
                int Buttons, int TextBytes, int ProjectedElements)
            {
                if (!GuiSchema.GetClass(Parent.ClassId).CanHaveChildren) throw new FacadeException("GUI parent cannot have children");
                if ((ChildClass == GuiClassId.UIListLayout || ChildClass == GuiClassId.UIPadding) && !GuiSchema.IsA(Parent.ClassId, "GuiObject"))
                    throw new FacadeException("GUI layout helpers require a GuiObject parent");
                if (ChildClass == GuiClassId.UIListLayout || ChildClass == GuiClassId.UIPadding) {
                    foreach (ulong SiblingId in Parent.Children) {
                        if (Child != null && SiblingId == Child.Identity.GuiObjectId) continue;
                        if (State.Nodes[SiblingId].ClassId == ChildClass) throw new FacadeException("GUI layout helper cardinality limit reached");
                    }
                }
                int RenderableChildren = 0;
                foreach (ulong ChildId in Parent.Children) if (GuiSchema.IsA(State.Nodes[ChildId].ClassId, "GuiObject")) RenderableChildren++;
                if (GuiSchema.IsA(ChildClass, "GuiObject") && RenderableChildren >= Limits.MaxChildrenPerObject)
                    throw new FacadeException("GUI child limit reached");
                for (GuiRetainedNode Cursor = Parent; Cursor != null; Cursor = Cursor.ParentId.HasValue ? State.Nodes[Cursor.ParentId.Value] : null)
                    if (Child != null && Cursor.Identity.GuiObjectId == Child.Identity.GuiObjectId) throw new FacadeException("GUI parent cycle rejected");
                int ParentDepth = DepthFromRoot(Parent);
                if (ParentDepth + Depth > Limits.MaxTreeDepth) throw new FacadeException("GUI tree depth limit reached");
                GuiRetainedNode Screen = RootScreen(Parent);
                if (Screen != null) {
                    int ExistingObjects = SubtreeCount(Screen); int ExistingButtons = SubtreeButtons(Screen); int ExistingText = SubtreeTextBytes(Screen);
                    int ExistingProjection = SubtreeProjectionCost(Screen);
                    GuiRetainedNode OldScreen = Child == null ? null : RootScreen(Child);
                    bool SameScreen = OldScreen != null && OldScreen.Identity.GuiObjectId == Screen.Identity.GuiObjectId;
                    if (!SameScreen && ExistingObjects + Objects > Limits.MaxObjectsPerScreen) throw new FacadeException("ScreenGui object limit reached");
                    if (!SameScreen && ExistingButtons + Buttons > Limits.MaxButtonsPerScreen) throw new FacadeException("ScreenGui button limit reached");
                    if (!SameScreen && ExistingText + TextBytes > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
                    if (!SameScreen && ExistingProjection + ProjectedElements > Limits.MaxProjectedElementsPerScreen)
                        throw new FacadeException("ScreenGui projection element limit reached");
                }
            }

            private static void Attach(GuiRetainedNode Child, GuiRetainedNode Parent)
            { Child.ParentId = Parent.Identity.GuiObjectId; Parent.Children.Add(Child.Identity.GuiObjectId); }

            private string[] ReadProperty(GuiRetainedNode Node, string Name)
            {
                GuiPropertyUse Use; if (!GuiSchema.TryGetProperty(Node.ClassId, Name, out Use)) throw new FacadeException("unknown GUI property");
                if (Use.Descriptor.Id == GuiPropertyId.ClassName) return new[] {"string", GuiSchema.GetClass(Node.ClassId).Name};
                if (Use.Descriptor.Id == GuiPropertyId.Parent) return Node.ParentId.HasValue ? ObjectValue(State.Nodes[Node.ParentId.Value]) : new[] {"nil"};
                return Node.Properties[Use.Descriptor.Id].Encode();
            }

            private GuiStoredValue ParseValue(GuiPropertyDescriptor Descriptor, string[] Fields, int KindIndex)
            {
                if (KindIndex >= Fields.Length) throw new FacadeException("missing GUI property value"); string Kind = Fields[KindIndex];
                switch (Descriptor.ValueKind) {
                    case GuiValueKind.String: {
                        if (Kind != "string" || Fields.Length != KindIndex + 2) throw new FacadeException("GUI property expects string");
                        int Limit = Descriptor.Utf8Limit == GuiLimitId.NameUtf8Bytes ? Limits.MaxNameUtf8Bytes :
                            Descriptor.Utf8Limit == GuiLimitId.TextUtf8Bytes ? Limits.MaxTextUtf8Bytes : Limits.MaxTextUtf8Bytes;
                        ValidateText(Fields[KindIndex + 1], Limit, Descriptor.Name);
                        string[] Allowed = Descriptor.AllowedValues;
                        if (Allowed.Length != 0 && Array.IndexOf(Allowed, Fields[KindIndex + 1]) < 0) throw new FacadeException("GUI property value is not allowed");
                        return GuiStoredValue.String(Fields[KindIndex + 1]);
                    }
                    case GuiValueKind.Boolean:
                        if (Kind != "boolean" || Fields.Length != KindIndex + 2 || (Fields[KindIndex + 1] != "0" && Fields[KindIndex + 1] != "1")) throw new FacadeException("GUI property expects boolean");
                        return GuiStoredValue.Bool(Fields[KindIndex + 1] == "1");
                    case GuiValueKind.Integer: {
                        int Value; if (Kind != "integer" || Fields.Length != KindIndex + 2 || !Int32.TryParse(Fields[KindIndex + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out Value))
                            throw new FacadeException("GUI property expects integer");
                        ValidateRange(Value, Descriptor.Minimum, Descriptor.Maximum, Descriptor.Name); return GuiStoredValue.Int(Value);
                    }
                    case GuiValueKind.Number: {
                        double Value = ParseNumber(Fields, KindIndex, "number", 1)[0]; ValidateRange(Value, Descriptor.Minimum, Descriptor.Maximum, Descriptor.Name); return GuiStoredValue.Number(Value);
                    }
                    case GuiValueKind.UDim: {
                        double[] Value = ParseNumber(Fields, KindIndex, "udim", 2);
                        ValidateRange(Value[0], Descriptor.Minimum ?? -8, Descriptor.Maximum ?? 8, Descriptor.Name);
                        bool NonNegative = Descriptor.Id == GuiPropertyId.PaddingTop || Descriptor.Id == GuiPropertyId.PaddingBottom ||
                            Descriptor.Id == GuiPropertyId.PaddingLeft || Descriptor.Id == GuiPropertyId.PaddingRight;
                        ValidateRange(Value[1], NonNegative ? 0 : -32768, 32768, Descriptor.Name);
                        return GuiStoredValue.UDim(Value[0], Value[1]);
                    }
                    case GuiValueKind.UDim2: {
                        double[] Value = ParseNumber(Fields, KindIndex, "udim2", 4); ValidateRange(Value[0], -8, 8, Descriptor.Name); ValidateRange(Value[2], -8, 8, Descriptor.Name);
                        ValidateRange(Value[1], -32768, 32768, Descriptor.Name); ValidateRange(Value[3], -32768, 32768, Descriptor.Name); return GuiStoredValue.UDim2(Value[0], Value[1], Value[2], Value[3]);
                    }
                    case GuiValueKind.Vector2: {
                        double[] Value = ParseNumber(Fields, KindIndex, "vector2", 2); double Minimum = Descriptor.Minimum ?? -32768, Maximum = Descriptor.Maximum ?? 32768;
                        ValidateRange(Value[0], Minimum, Maximum, Descriptor.Name); ValidateRange(Value[1], Minimum, Maximum, Descriptor.Name); return GuiStoredValue.Vector2(Value[0], Value[1]);
                    }
                    case GuiValueKind.Color3: {
                        double[] Value = ParseNumber(Fields, KindIndex, "color3", 3); foreach (double Component in Value) ValidateRange(Component, 0, 1, Descriptor.Name);
                        return GuiStoredValue.Color3(Value[0], Value[1], Value[2]);
                    }
                    case GuiValueKind.ImageSource:
                        return GuiStoredValue.Image(GuiImageSourceValue.Parse(Fields, KindIndex));
                    default: throw new FacadeException("unsupported GUI property type");
                }
            }

            private static double[] ParseNumber(string[] Fields, int KindIndex, string Kind, int Count)
            {
                if (Fields[KindIndex] != Kind || Fields.Length != KindIndex + Count + 1) throw new FacadeException("GUI property value has the wrong type");
                var Result = new double[Count];
                for (int Index = 0; Index < Count; ++Index) {
                    if (!Double.TryParse(Fields[KindIndex + Index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out Result[Index]) || Double.IsNaN(Result[Index]) || Double.IsInfinity(Result[Index]))
                        throw new FacadeException("GUI numeric value must be finite");
                    if (Result[Index] == 0) Result[Index] = 0;
                }
                return Result;
            }
            private static void ValidateRange(double Value, double? Minimum, double? Maximum, string Name)
            { if ((Minimum.HasValue && Value < Minimum.Value) || (Maximum.HasValue && Value > Maximum.Value)) throw new FacadeException(Name + " is outside its allowed range"); }
            private static void ValidateText(string Value, int Limit, string Name) { FacadePolicy.Text(Value, Limit, Name); }

            private void ApplyDefaults(GuiRetainedNode Node)
            {
                string ClassName = GuiSchema.GetClass(Node.ClassId).Name; Node.Properties[GuiPropertyId.Name] = GuiStoredValue.String(ClassName);
                if (Node.ClassId == GuiClassId.ScreenGui) return;
                if (Node.ClassId == GuiClassId.UIListLayout) {
                    Node.Properties[GuiPropertyId.Padding] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.FillDirection] = GuiStoredValue.String("Vertical");
                    Node.Properties[GuiPropertyId.HorizontalAlignment] = GuiStoredValue.String("Left");
                    Node.Properties[GuiPropertyId.VerticalAlignment] = GuiStoredValue.String("Top");
                    return;
                }
                if (Node.ClassId == GuiClassId.UIPadding) {
                    Node.Properties[GuiPropertyId.PaddingTop] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingBottom] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingLeft] = GuiStoredValue.UDim(0, 0);
                    Node.Properties[GuiPropertyId.PaddingRight] = GuiStoredValue.UDim(0, 0);
                    return;
                }
                Node.Properties[GuiPropertyId.Position] = GuiStoredValue.UDim2(0, 0, 0, 0);
                int Height = Node.ClassId == GuiClassId.Frame || Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton
                    ? 100 : Node.ClassId == GuiClassId.TextLabel ? 30 : 36;
                Node.Properties[GuiPropertyId.Size] = GuiStoredValue.UDim2(0, 100, 0, Height);
                Node.Properties[GuiPropertyId.AnchorPoint] = GuiStoredValue.Vector2(0, 0);
                Node.Properties[GuiPropertyId.Visible] = GuiStoredValue.Bool(true);
                Node.Properties[GuiPropertyId.BackgroundColor3] = GuiStoredValue.Color3(1, 1, 1);
                Node.Properties[GuiPropertyId.BackgroundTransparency] = GuiStoredValue.Number(
                    Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton ? 1 : 0);
                Node.Properties[GuiPropertyId.ZIndex] = GuiStoredValue.Int(1);
                Node.Properties[GuiPropertyId.LayoutOrder] = GuiStoredValue.Int(0);
                if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                    Node.Properties[GuiPropertyId.Text] = GuiStoredValue.String(""); Node.Properties[GuiPropertyId.TextColor3] = GuiStoredValue.Color3(0, 0, 0);
                    Node.Properties[GuiPropertyId.TextTransparency] = GuiStoredValue.Number(0); Node.Properties[GuiPropertyId.TextSize] = GuiStoredValue.Int(14);
                    Node.Properties[GuiPropertyId.TextXAlignment] = GuiStoredValue.String("Center"); Node.Properties[GuiPropertyId.TextYAlignment] = GuiStoredValue.String("Center");
                }
                if (Node.ClassId == GuiClassId.ImageLabel || Node.ClassId == GuiClassId.ImageButton) {
                    Node.Properties[GuiPropertyId.Image] = GuiStoredValue.Image(GuiImageSourceValue.Parse(new[] {"imagesource", "None"}, 0));
                    Node.Properties[GuiPropertyId.ImageColor3] = GuiStoredValue.Color3(1, 1, 1);
                    Node.Properties[GuiPropertyId.ImageTransparency] = GuiStoredValue.Number(0);
                }
            }

            private GuiRetainedNode Node(ulong Id)
            {
                GuiRetainedNode Result; if (State.Nodes.TryGetValue(Id, out Result)) return Result;
                if (Id > 0 && Id < NextObjectId) throw new FacadeException("GUI object is destroyed");
                throw new FacadeException("unknown GUI object identity");
            }
            private static ulong Id(string Value) { ulong Result; if (!UInt64.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out Result) || Result == 0) throw new FacadeException("invalid GUI object identity"); return Result; }
            private static void RequireFields(string[] Fields, int Count) { if (Fields.Length != Count) throw new FacadeException("invalid GUI operation arguments"); }
            private static string[] ObjectResult(GuiRetainedNode Node) { return new[] {Node.Identity.GuiObjectId.ToString(CultureInfo.InvariantCulture), GuiSchema.GetClass(Node.ClassId).Name}; }
            private static string[] ObjectValue(GuiRetainedNode Node) { return new[] {"object", Node.Identity.GuiObjectId.ToString(CultureInfo.InvariantCulture), GuiSchema.GetClass(Node.ClassId).Name}; }
            private void Collect(GuiRetainedNode Root, List<ulong> Result) { Result.Add(Root.Identity.GuiObjectId); foreach (ulong Child in Root.Children) Collect(State.Nodes[Child], Result); }
            private int SubtreeCount(GuiRetainedNode Root) { int Result = 1; foreach (ulong Child in Root.Children) Result += SubtreeCount(State.Nodes[Child]); return Result; }
            private int SubtreeDepth(GuiRetainedNode Root) { int Result = 1; foreach (ulong Child in Root.Children) Result = Math.Max(Result, 1 + SubtreeDepth(State.Nodes[Child])); return Result; }
            private int SubtreeButtons(GuiRetainedNode Root) { int Result = IsActivatedClass(Root.ClassId) ? 1 : 0; foreach (ulong Child in Root.Children) Result += SubtreeButtons(State.Nodes[Child]); return Result; }
            private int SubtreeProjectionCost(GuiRetainedNode Root)
            { int Result = ProjectionCost(Root.ClassId); foreach (ulong Child in Root.Children) Result += SubtreeProjectionCost(State.Nodes[Child]); return Result; }
            private static int ProjectionCost(GuiClassId ClassId)
            {
                if (ClassId == GuiClassId.ScreenGui || ClassId == GuiClassId.Frame) return 1;
                if (ClassId == GuiClassId.TextLabel || ClassId == GuiClassId.TextButton || ClassId == GuiClassId.ImageLabel) return 2;
                if (ClassId == GuiClassId.ImageButton) return 3;
                return 0;
            }
            private static bool IsActivatedClass(GuiClassId ClassId)
            { return ClassId == GuiClassId.TextButton || ClassId == GuiClassId.ImageButton; }
            private int SubtreeTextBytes(GuiRetainedNode Root)
            {
                int Result = 0; GuiStoredValue Text; if (Root.Properties.TryGetValue(GuiPropertyId.Text, out Text)) Result = FacadePolicy.Utf8.GetByteCount(Text.Text);
                foreach (ulong Child in Root.Children) Result += SubtreeTextBytes(State.Nodes[Child]); return Result;
            }
            private int TextBytes(GuiClassId ClassId) { return 0; }
            private bool HasListParent(GuiRetainedNode Node)
            {
                if (!Node.ParentId.HasValue) return false;
                foreach (ulong ChildId in State.Nodes[Node.ParentId.Value].Children)
                    if (State.Nodes[ChildId].ClassId == GuiClassId.UIListLayout) return true;
                return false;
            }
            private int ScreenTextBytes(GuiRetainedNode Screen) { return SubtreeTextBytes(Screen); }
            private int DepthFromRoot(GuiRetainedNode Node) { int Result = 1; while (Node.ParentId.HasValue) { Result++; Node = State.Nodes[Node.ParentId.Value]; } return Result; }
            private GuiRetainedNode RootScreen(GuiRetainedNode Node)
            {
                while (Node.ParentId.HasValue) Node = State.Nodes[Node.ParentId.Value]; return Node.ClassId == GuiClassId.ScreenGui ? Node : null;
            }
            private void RequireLive() { if (Disposed) throw new FacadeException("stale GUI domain lifetime"); }
            public void Dispose()
            {
                if (Disposed) return; Disposed = true;
                World.ReconcileActions(this, null, GuiActionRejection.Stale);
                var Destroyed = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiPresentation Value in State.Presentations.Values) {
                    World.RemovePresentation(Value.PlayerToken);
                    if (Value.WasSent && Destroyed.Add(Value.Target.Key)) TryDestroy(Value.Target);
                }
                foreach (GuiBackendTarget Value in State.PendingDestroys) if (Destroyed.Add(Value.Key)) TryDestroy(Value);
                World.Adjust(-State.Nodes.Count); State = new GuiRetainedState(); Publications.Clear();
                World.UnregisterRegistry(this);
            }
            private void TryDestroy(GuiBackendTarget Target)
            {
                try { if (!World.Backend.Destroy(Target).Accepted) World.BackendFailed(); }
                catch { World.BackendFailed(); }
            }
        }
    }
}
