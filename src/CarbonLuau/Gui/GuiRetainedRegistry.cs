using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {

        internal sealed class GuiRetainedState : GuiTreeState
        {
            internal readonly Dictionary<string, ulong> Connections = new Dictionary<string, ulong>(StringComparer.Ordinal);
            internal readonly Dictionary<string, GuiPresentation> Presentations = new Dictionary<string, GuiPresentation>(StringComparer.Ordinal);
            internal readonly Dictionary<ulong, GuiScreenSynchronization> Synchronization = new Dictionary<ulong, GuiScreenSynchronization>();
            internal readonly Dictionary<string, GuiScrollIntent> StagedScrollEffects = new Dictionary<string, GuiScrollIntent>(StringComparer.Ordinal);
            internal readonly Queue<GuiBackendTarget> PendingDestroys = new Queue<GuiBackendTarget>();
            internal override GuiTreeState CopyTree() { return Copy(); }
            internal GuiRetainedState Copy()
            {
                var Result = new GuiRetainedState {Screens = Screens};
                foreach (var Value in Nodes) Result.Nodes.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in Connections) Result.Connections.Add(Value.Key, Value.Value);
                foreach (var Value in Presentations) Result.Presentations.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in Synchronization) Result.Synchronization.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in StagedScrollEffects) Result.StagedScrollEffects.Add(Value.Key, Value.Value);
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
            internal readonly GuiScrollDiagnostics ScrollDiagnostics = new GuiScrollDiagnostics();
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
            internal int PendingScrollEffectCount {
                get { int Result = 0; foreach (GuiRetainedRegistry Registry in Registries) Result += Registry.PendingScrollEffectCount; return Result; }
            }
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

            internal void EnsureScrollCapacity(GuiRetainedRegistry Registry, ulong ScreenId, string PlayerToken, ulong ScrollFrameId)
            {
                if (Registry == null) throw new FacadeException("invalid scroll-effect owner");
                if (Registry.HasScrollEffect(ScreenId, PlayerToken, ScrollFrameId)) return;
                if (Registry.PendingScrollEffectsForPresentation(ScreenId, PlayerToken) >= Limits.MaxPendingScrollEffectsPerPresentation ||
                    Registry.PendingScrollEffectCount >= Limits.MaxPendingScrollEffectsPerDomain ||
                    PendingScrollEffectCount >= Limits.MaxPendingScrollEffectsGlobal) {
                    ScrollDiagnostics.RejectBound(); RuntimeDiagnostics.ResourceLimitRejected();
                    throw new FacadeException("GUI pending scroll-effect limit reached");
                }
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
                    int Screens = 0, Connections = 0, Dirty = 0, FullResync = 0, Blocked = 0, PendingDestroys = 0, PendingScroll = 0;
                    foreach (GuiRetainedRegistry Registry in Registries) {
                        Screens += Registry.ScreenCount; Connections += Registry.ConnectionCount;
                        Dirty += Registry.DirtyPresentationCount; FullResync += Registry.FullResyncPresentationCount;
                        Blocked += Registry.BlockedPresentationCount; PendingDestroys += Registry.PendingDestroyCount;
                        PendingScroll += Registry.PendingScrollEffectCount;
                    }
                    return "GUI live registries/objects/screens/presentations/connections/actions: " + Registries.Count + "/" +
                        LiveObjects + "/" + Screens + "/" + LivePresentations + "/" + Connections + "/" + Actions.Count +
                        "\nGUI presentation dirty/full-resync/blocked/pending-destroy: " + Dirty + "/" + FullResync + "/" + Blocked + "/" + PendingDestroys +
                        "\nGUI pending scroll effects: " + PendingScroll +
                        "\nGUI backend full/patch/failures; resource-limit rejections: " + RuntimeDiagnostics.FullRebuilds + "/" +
                        RuntimeDiagnostics.Patches + "/" + RuntimeDiagnostics.BackendSendFailures + "; " + RuntimeDiagnostics.ResourceLimitRejections +
                        "\n" + ScrollDiagnostics.Status() + "\n" + ActionStatus;
                }
            }
            private static bool CanonicalToken(string Token)
            {
                if (Token == null || Token.Length != 32) return false;
                foreach (char Value in Token) if (!((Value >= '0' && Value <= '9') || (Value >= 'a' && Value <= 'f'))) return false;
                return true;
            }
        }

        internal sealed class GuiRetainedRegistry : GuiTree<GuiRetainedState>, IGuiRetainedRegistry, IDisposable
        {
            private readonly GuiRetainedWorld World;
            private readonly Stack<GuiRetainedState> Publications = new Stack<GuiRetainedState>();
            private ulong NextPresentationEpoch = 1;
            private string PresentationFlushCursor;
            private bool PreferDestroy;

            internal GuiRetainedRegistry(GuiRetainedWorld World, ulong VmGenerationId, ulong DomainLifetimeId)
                : base(new GuiRetainedState(), World == null ? null : World.Limits, VmGenerationId, DomainLifetimeId)
            {
                if (VmGenerationId == 0 || DomainLifetimeId == 0) throw new ArgumentOutOfRangeException("GUI owner identity");
                this.World = World ?? throw new ArgumentNullException("World");
                World.RegisterRegistry(this);
            }
            internal int LiveObjectCount { get { return State.Nodes.Count; } }
            internal int ScreenCount { get { return State.Screens; } }
            internal int ConnectionCount { get { return State.Connections.Count; } }
            internal int PresentationCount { get { return State.Presentations.Count; } }
            internal int PublicationDepth { get { return Publications.Count; } }
            internal int PendingDestroyCount { get { return State.PendingDestroys.Count; } }
            internal int PendingScrollEffectCount { get { return CountScrollEffects(); } }
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
                if (Publications.Count == 0) { ReconcileCommittedActions(); PublishCommittedScrollEffects(); }
            }
            internal void RollbackPublication()
            {
                RequireLive(); if (Publications.Count == 0) throw new FacadeException("invalid GUI publication rollback");
                GuiRetainedState Previous = Publications.Pop(); World.Restore(Previous.Nodes.Count - State.Nodes.Count);
                World.RestorePresentations(State.Presentations.Values, Previous.Presentations.Values); State = Previous;
            }

            protected override void AdjustObjectCount(int Change) { World.Adjust(Change); }
            protected override void ScreenCreated(ulong ScreenId) { State.Synchronization.Add(ScreenId, new GuiScreenSynchronization()); }
            protected override void ScreenDestroyed(ulong ScreenId) { State.Synchronization.Remove(ScreenId); }
            protected override void TreeChanged(GuiRetainedNode Screen, bool TargetUnavailable = false)
            { MarkStructural(Screen, TargetUnavailable ? GuiActionRejection.TargetUnavailable : GuiActionRejection.Stale); }
            protected override void BeforeDetach(List<ulong> MovingIds) { DiscardScrollEffectsForNodes(MovingIds); }
            protected override string[] BeforeDestroy(GuiRetainedNode Root, List<ulong> RemovedIds)
            {
                DiscardScrollEffectsForNodes(RemovedIds);
                if (Root.ClassId == GuiClassId.ScreenGui) RemovePresentationsForScreen(Root.Identity.GuiObjectId);
                var RemovedConnections = new List<string>();
                foreach (ulong RemovedId in RemovedIds)
                    foreach (var Connection in new List<KeyValuePair<string, ulong>>(State.Connections))
                        if (Connection.Value == RemovedId) { RemovedConnections.Add(Connection.Key); State.Connections.Remove(Connection.Key); }
                return RemovedConnections.ToArray();
            }
            protected override void PropertyChanged(GuiRetainedNode Node, GuiPropertyUse Use)
            {
                GuiRetainedNode ScreenValue = RootScreen(Node);
                if (CouldAffectClippedInteraction(Use.Descriptor.Id, ScreenValue))
                    MarkStructural(ScreenValue, GuiActionRejection.TargetUnavailable);
                else if (Use.Descriptor.Id == GuiPropertyId.Visible && ContainsConnectedButton(Node))
                    MarkStructural(ScreenValue, GuiActionRejection.TargetUnavailable);
                else if (Use.Descriptor.MutationKind == GuiMutationKind.Structural) MarkStructural(ScreenValue);
                else if (Use.Descriptor.MutationKind == GuiMutationKind.Patchable) {
                    bool GridManaged = HasGridParent(Node);
                    if (GridManaged && (Use.Descriptor.Id == GuiPropertyId.Position || Use.Descriptor.Id == GuiPropertyId.Size)) return;
                    if (GridManaged && Use.Descriptor.Id == GuiPropertyId.AnchorPoint)
                        MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                    else {
                        MarkPatchable(ScreenValue, Node.Identity.GuiObjectId, Use.Descriptor.Id);
                        if ((Use.Descriptor.Id == GuiPropertyId.Size || Use.Descriptor.Id == GuiPropertyId.Visible) && HasLayoutParent(Node))
                            MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                    }
                } else if (Use.Descriptor.MutationKind == GuiMutationKind.LayoutAffecting) {
                    if (IsLayoutHelper(Node.ClassId)) {
                        if (Node.ParentId.HasValue) MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                    } else if (Use.Descriptor.Id == GuiPropertyId.LayoutOrder && HasLayoutParent(Node))
                        MarkLayoutAffected(State.Nodes[Node.ParentId.Value]);
                }
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
                    case "scroll": {
                        RequireFields(Fields, 6); double X = ScrollCoordinate(Fields[4]), Y = ScrollCoordinate(Fields[5]);
                        StageScroll(Node(Id(Fields[1])), Fields[2], Fields[3], GuiScrollIntentKind.Position, X, Y); return new string[0];
                    }
                    case "scrolltop": RequireFields(Fields, 4); StageScroll(Node(Id(Fields[1])), Fields[2], Fields[3], GuiScrollIntentKind.Top, 0, 0); return new string[0];
                    case "scrollbottom": RequireFields(Fields, 4); StageScroll(Node(Id(Fields[1])), Fields[2], Fields[3], GuiScrollIntentKind.Bottom, 0, 1); return new string[0];
                    default: throw new FacadeException("unknown GUI mutation");
                } } catch (FacadeException Error) { World.RecordResourceLimit(Error); throw; }
            }

            private void StageScroll(GuiRetainedNode ScrollFrame, string PlayerToken, string PlayerUserId,
                GuiScrollIntentKind Kind, double X, double Y)
            {
                if (ScrollFrame.ClassId != GuiClassId.ScrollingFrame) throw new FacadeException("scroll methods require ScrollingFrame");
                GuiRetainedNode Screen = RootScreen(ScrollFrame);
                if (Screen == null) throw new FacadeException("ScrollingFrame has no containing ScreenGui");
                PlayerLifetime Player = World.ExactPlayer(PlayerToken, PlayerUserId);
                if (Player == null) throw new FacadeException("Player is no longer connected");
                GuiPresentation Presentation;
                if (!State.Presentations.TryGetValue(PresentationKey(Screen.Identity.GuiObjectId, PlayerToken), out Presentation) ||
                    Presentation.PlayerUserId != PlayerUserId)
                    throw new FacadeException("ScrollingFrame has no current Presentation for Player");
                string Direction = ScrollFrame.Properties[GuiPropertyId.ScrollingDirection].Text;
                if ((Kind == GuiScrollIntentKind.Top || Kind == GuiScrollIntentKind.Bottom) && Direction != "Y" && Direction != "XY")
                    throw new FacadeException("vertical scrolling is unavailable");
                var Intent = new GuiScrollIntent(Screen.Identity.GuiObjectId, ScrollFrame.Identity.GuiObjectId, Player, Kind, X, Y);
                World.EnsureScrollCapacity(this, Intent.ScreenId, Intent.PlayerToken, Intent.ScrollFrameId);
                bool Replaced = HasScrollEffect(Intent.ScreenId, Intent.PlayerToken, Intent.ScrollFrameId);
                if (Publications.Count != 0) State.StagedScrollEffects[Intent.Key] = Intent;
                else Presentation.PendingScrollEffects[Intent.ScrollFrameId] = Intent;
                World.ScrollDiagnostics.Accept(Replaced);
            }

            private void PublishCommittedScrollEffects()
            {
                if (State.StagedScrollEffects.Count == 0) return;
                var Values = new List<GuiScrollIntent>(State.StagedScrollEffects.Values); State.StagedScrollEffects.Clear();
                Values.Sort((Left, Right) => StringComparer.Ordinal.Compare(Left.Key, Right.Key));
                foreach (GuiScrollIntent Intent in Values) {
                    PlayerLifetime Player = World.ExactPlayer(Intent.PlayerToken, Intent.PlayerUserId);
                    if (Player == null || !Object.ReferenceEquals(Player.Identity, Intent.PlayerIdentity) ||
                        !Object.ReferenceEquals(Player.Connection, Intent.PlayerConnection)) {
                        World.ScrollDiagnostics.DiscardPlayer(); continue;
                    }
                    GuiPresentation Presentation; GuiRetainedNode ScrollFrame;
                    if (!State.Presentations.TryGetValue(PresentationKey(Intent.ScreenId, Intent.PlayerToken), out Presentation) ||
                        !State.Nodes.TryGetValue(Intent.ScrollFrameId, out ScrollFrame) || ScrollFrame.ClassId != GuiClassId.ScrollingFrame ||
                        RootScreen(ScrollFrame) == null || RootScreen(ScrollFrame).Identity.GuiObjectId != Intent.ScreenId) {
                        World.ScrollDiagnostics.DiscardPresentation(); continue;
                    }
                    Presentation.PendingScrollEffects[Intent.ScrollFrameId] = Intent;
                }
            }

            internal bool HasScrollEffect(ulong ScreenId, string PlayerToken, ulong ScrollFrameId)
            {
                GuiPresentation Presentation;
                if (State.Presentations.TryGetValue(PresentationKey(ScreenId, PlayerToken), out Presentation) &&
                    Presentation.PendingScrollEffects.ContainsKey(ScrollFrameId)) return true;
                foreach (GuiScrollIntent Value in State.StagedScrollEffects.Values)
                    if (Value.ScreenId == ScreenId && Value.PlayerToken == PlayerToken && Value.ScrollFrameId == ScrollFrameId) return true;
                return false;
            }

            internal int PendingScrollEffectsForPresentation(ulong ScreenId, string PlayerToken)
            {
                var Values = new HashSet<ulong>(); GuiPresentation Presentation;
                if (State.Presentations.TryGetValue(PresentationKey(ScreenId, PlayerToken), out Presentation))
                    foreach (ulong Value in Presentation.PendingScrollEffects.Keys) Values.Add(Value);
                foreach (GuiScrollIntent Value in State.StagedScrollEffects.Values)
                    if (Value.ScreenId == ScreenId && Value.PlayerToken == PlayerToken) Values.Add(Value.ScrollFrameId);
                return Values.Count;
            }

            private int CountScrollEffects()
            {
                var Values = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiPresentation Presentation in State.Presentations.Values)
                    foreach (ulong ScrollFrameId in Presentation.PendingScrollEffects.Keys)
                        Values.Add(ScrollEffectKey(Presentation.ScreenId, Presentation.PlayerToken, ScrollFrameId));
                foreach (GuiScrollIntent Intent in State.StagedScrollEffects.Values) Values.Add(Intent.Key);
                return Values.Count;
            }

            private static string ScrollEffectKey(ulong ScreenId, string PlayerToken, ulong ScrollFrameId)
            {
                return ScreenId.ToString(CultureInfo.InvariantCulture) + ":" + PlayerToken.Length.ToString(CultureInfo.InvariantCulture) +
                    ":" + PlayerToken + ":" + ScrollFrameId.ToString(CultureInfo.InvariantCulture);
            }

            private static double ScrollCoordinate(string Value)
            {
                double Result;
                if (!Double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out Result))
                    throw new FacadeException("scroll coordinates must be finite and within 0..1");
                GuiScrollIntent.ValidateCoordinate(Result); return Result == 0 ? 0 : Result;
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
                DiscardPresentationScrollEffects(Presentation, false);
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
                    DiscardPresentationScrollEffects(Value, true);
                    InvalidatePresentation(Value, GuiActionRejection.Stale); World.RemovePresentation(Value.PlayerToken); State.Presentations.Remove(Key);
                }
                var StagedKeys = new List<string>(); foreach (var Value in State.StagedScrollEffects)
                    if (Value.Value.PlayerToken == Player.Token) StagedKeys.Add(Value.Key);
                foreach (string Key in StagedKeys) { State.StagedScrollEffects.Remove(Key); World.ScrollDiagnostics.DiscardPlayer(); }
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
                    DiscardPresentationScrollEffects(Selected, true);
                    InvalidatePresentation(Selected, GuiActionRejection.Stale); State.Presentations.Remove(SelectedKey);
                    World.RemovePresentation(Selected.PlayerToken); PruneSynchronization(Selected.ScreenId); return 0;
                }
                GuiRetainedNode Screen;
                if (!State.Nodes.TryGetValue(Selected.ScreenId, out Screen)) {
                    DiscardPresentationScrollEffects(Selected, false);
                    InvalidatePresentation(Selected, GuiActionRejection.TargetUnavailable); State.Presentations.Remove(SelectedKey);
                    World.RemovePresentation(Selected.PlayerToken); State.Synchronization.Remove(Selected.ScreenId); return 0;
                }
                GuiScreenSynchronization Synchronization = State.Synchronization[Selected.ScreenId]; ulong Revision = Synchronization.Revision;
                if (!PresentationNeedsRetainedWork(Selected)) return FlushScrollEffect(Selected, Screen, RemainingBytes);
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
            private int FlushScrollEffect(GuiPresentation Presentation, GuiRetainedNode Screen, int RemainingBytes)
            {
                GuiScrollIntent Intent = SelectScrollEffect(Presentation);
                if (Intent == null) return Int32.MinValue;
                PlayerLifetime Player = World.ExactPlayer(Intent.PlayerToken, Intent.PlayerUserId);
                if (Player == null || !Object.ReferenceEquals(Player.Identity, Intent.PlayerIdentity) ||
                    !Object.ReferenceEquals(Player.Connection, Intent.PlayerConnection)) {
                    Presentation.PendingScrollEffects.Remove(Intent.ScrollFrameId); World.ScrollDiagnostics.DiscardPlayer(); return 0;
                }
                GuiRetainedNode ScrollFrame;
                if (!State.Nodes.TryGetValue(Intent.ScrollFrameId, out ScrollFrame) || ScrollFrame.ClassId != GuiClassId.ScrollingFrame ||
                    RootScreen(ScrollFrame) != Screen) {
                    Presentation.PendingScrollEffects.Remove(Intent.ScrollFrameId); World.ScrollDiagnostics.DiscardPresentation(); return 0;
                }
                string Direction = ScrollFrame.Properties[GuiPropertyId.ScrollingDirection].Text;
                double? Horizontal = null, Vertical = null;
                if (Intent.Kind == GuiScrollIntentKind.Position) {
                    if (Direction == "X" || Direction == "XY") Horizontal = Intent.X;
                    if (Direction == "Y" || Direction == "XY") Vertical = Intent.Y;
                } else {
                    if (Direction != "Y" && Direction != "XY") {
                        Presentation.PendingScrollEffects.Remove(Intent.ScrollFrameId); World.ScrollDiagnostics.DiscardPresentation(); return 0;
                    }
                    Vertical = Intent.Kind == GuiScrollIntentKind.Top ? 0 : 1;
                }
                var Effect = new GuiScrollEffect(Presentation.ObjectClientId(Intent.ScrollFrameId), Presentation.Epoch,
                    Intent.ScrollFrameId, Horizontal, Vertical);
                int Bytes;
                try { Bytes = World.Backend.MeasureScroll(Presentation.Target, Effect); }
                catch {
                    World.BackendFailed(); World.ScrollDiagnostics.BackendFailed(); RequireFullRebuild(Presentation);
                    Presentation.SynchronizationUncertain = true; return 0;
                }
                if (Bytes > RemainingBytes) return -Bytes;
                GuiBackendResult Result;
                try { Result = World.Backend.Scroll(Presentation.Target, Effect); }
                catch { Result = GuiBackendResult.Failure(GuiBackendResultCode.SendFailed, "GUI backend scroll send failed"); }
                if (Result.Accepted) {
                    Presentation.PendingScrollEffects.Remove(Intent.ScrollFrameId); World.ScrollDiagnostics.SendAccepted();
                } else {
                    World.BackendFailed(); World.ScrollDiagnostics.BackendFailed(); RequireFullRebuild(Presentation);
                    Presentation.SynchronizationUncertain = true; Presentation.SuccessfulPatchBatches = 0;
                }
                return Bytes;
            }
            private static GuiScrollIntent SelectScrollEffect(GuiPresentation Presentation)
            {
                GuiScrollIntent Selected = null;
                foreach (var Value in Presentation.PendingScrollEffects) if (Value.Key > Presentation.ScrollEffectFlushCursor) { Selected = Value.Value; break; }
                if (Selected == null) foreach (var Value in Presentation.PendingScrollEffects) { Selected = Value.Value; break; }
                if (Selected != null) Presentation.ScrollEffectFlushCursor = Selected.ScrollFrameId;
                return Selected;
            }
            private void RemovePresentationsForScreen(ulong ScreenId)
            {
                var Keys = new List<string>(); foreach (var Value in State.Presentations) if (Value.Value.ScreenId == ScreenId) Keys.Add(Value.Key);
                foreach (string Key in Keys) {
                    GuiPresentation Presentation = State.Presentations[Key]; State.Presentations.Remove(Key); World.RemovePresentation(Presentation.PlayerToken);
                    DiscardPresentationScrollEffects(Presentation, false);
                    InvalidatePresentation(Presentation, GuiActionRejection.TargetUnavailable);
                    if (Presentation.WasSent) State.PendingDestroys.Enqueue(Presentation.Target);
                }
                PruneSynchronization(ScreenId);
            }
            private void DiscardPresentationScrollEffects(GuiPresentation Presentation, bool PlayerStale)
            {
                if (Presentation == null || Presentation.PendingScrollEffects.Count == 0) return;
                int Count = Presentation.PendingScrollEffects.Count; Presentation.PendingScrollEffects.Clear();
                if (PlayerStale) World.ScrollDiagnostics.DiscardPlayer(Count); else World.ScrollDiagnostics.DiscardPresentation(Count);
            }
            private void DiscardScrollEffectsForNodes(ICollection<ulong> NodeIds)
            {
                if (NodeIds == null || NodeIds.Count == 0) return;
                var Removed = new HashSet<ulong>(NodeIds);
                foreach (GuiPresentation Presentation in State.Presentations.Values) {
                    var Keys = new List<ulong>(); foreach (ulong Key in Presentation.PendingScrollEffects.Keys) if (Removed.Contains(Key)) Keys.Add(Key);
                    foreach (ulong Key in Keys) Presentation.PendingScrollEffects.Remove(Key);
                    World.ScrollDiagnostics.DiscardPresentation(Keys.Count);
                }
                var StagedKeys = new List<string>(); foreach (var Value in State.StagedScrollEffects)
                    if (Removed.Contains(Value.Value.ScrollFrameId)) StagedKeys.Add(Value.Key);
                foreach (string Key in StagedKeys) { State.StagedScrollEffects.Remove(Key); World.ScrollDiagnostics.DiscardPresentation(); }
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
                return PresentationNeedsRetainedWork(Presentation) || (Presentation.WasSent && Presentation.PendingScrollEffects.Count != 0);
            }
            private bool PresentationNeedsRetainedWork(GuiPresentation Presentation)
            {
                GuiScreenSynchronization Synchronization;
                return State.Synchronization.TryGetValue(Presentation.ScreenId, out Synchronization) &&
                    (Presentation.NeedsFullResync || Presentation.SentRevision < Synchronization.Revision);
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
            private bool CouldAffectClippedInteraction(GuiPropertyId PropertyId, GuiRetainedNode Screen)
            {
                if (Screen == null || !ContainsClippingFrame(Screen) || !ContainsConnectedButton(Screen)) return false;
                if (PropertyId == GuiPropertyId.Position || PropertyId == GuiPropertyId.Size || PropertyId == GuiPropertyId.AnchorPoint ||
                    PropertyId == GuiPropertyId.LayoutOrder || PropertyId == GuiPropertyId.Padding || PropertyId == GuiPropertyId.FillDirection ||
                    PropertyId == GuiPropertyId.HorizontalAlignment || PropertyId == GuiPropertyId.VerticalAlignment ||
                    PropertyId == GuiPropertyId.PaddingTop || PropertyId == GuiPropertyId.PaddingBottom ||
                    PropertyId == GuiPropertyId.PaddingLeft || PropertyId == GuiPropertyId.PaddingRight ||
                    PropertyId == GuiPropertyId.CellSize || PropertyId == GuiPropertyId.CellPadding ||
                    PropertyId == GuiPropertyId.FillDirectionMaxCells) return true;
                return false;
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
                    !GuiRenderCompiler.IsEffectivelyInteractive(State, Screen, Button)) return GuiActionRejection.TargetUnavailable;
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

            protected override void ValidateClippingProjectionCandidate(GuiTreeState Tree, ulong ScreenId)
            {
                var Candidate = (GuiRetainedState)Tree;
                GuiPresentation Presentation = null;
                foreach (GuiPresentation Value in Candidate.Presentations.Values)
                    if (Value.ScreenId == ScreenId) { Presentation = Value; break; }
                if (Presentation == null) Presentation = new GuiPresentation(ScreenId, 1, "measure", "measure",
                    "cluau_00000000000000000000000000000000_");
                try {
                    GuiRenderPlan Plan = GuiRenderCompiler.Compile(Candidate, Candidate.Nodes[ScreenId], Presentation, Limits, Button => {
                        foreach (ulong Owner in Candidate.Connections.Values)
                            if (Owner == Button.Identity.GuiObjectId) return GuiRetainedWorld.ActionCommand + " " + new string('0', 32);
                        return null;
                    });
                    World.Backend.MeasureReplace(Presentation.Target, Plan);
                } catch (FacadeException) { throw; }
                catch (Exception) { throw new FacadeException("GUI clipping projection exceeds the authoritative render envelope"); }
            }
            public void Dispose()
            {
                if (Disposed) return; Disposed = true;
                int PendingScroll = CountScrollEffects(); if (PendingScroll != 0) World.ScrollDiagnostics.DiscardPresentation(PendingScroll);
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
