using System;
using System.Collections.Generic;
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
            private readonly double[] NumberValues;
            internal double[] Numbers { get { return NumberValues == null ? null : (double[])NumberValues.Clone(); } }

            private GuiStoredValue(GuiValueKind Kind, string Text = null, bool Boolean = false, int Integer = 0, params double[] Numbers)
            { this.Kind = Kind; this.Text = Text; this.Boolean = Boolean; this.Integer = Integer; NumberValues = Numbers; }
            internal static GuiStoredValue String(string Value) { return new GuiStoredValue(GuiValueKind.String, Text: Value); }
            internal static GuiStoredValue Bool(bool Value) { return new GuiStoredValue(GuiValueKind.Boolean, Boolean: Value); }
            internal static GuiStoredValue Int(int Value) { return new GuiStoredValue(GuiValueKind.Integer, Integer: Value); }
            internal static GuiStoredValue Number(double Value) { return new GuiStoredValue(GuiValueKind.Number, Numbers: new[] {Normalize(Value)}); }
            internal static GuiStoredValue UDim(double Scale, double Offset) { return new GuiStoredValue(GuiValueKind.UDim, Numbers: new[] {Normalize(Scale), Normalize(Offset)}); }
            internal static GuiStoredValue UDim2(double XScale, double XOffset, double YScale, double YOffset)
            { return new GuiStoredValue(GuiValueKind.UDim2, Numbers: new[] {Normalize(XScale), Normalize(XOffset), Normalize(YScale), Normalize(YOffset)}); }
            internal static GuiStoredValue Vector2(double X, double Y) { return new GuiStoredValue(GuiValueKind.Vector2, Numbers: new[] {Normalize(X), Normalize(Y)}); }
            internal static GuiStoredValue Color3(double R, double G, double B) { return new GuiStoredValue(GuiValueKind.Color3, Numbers: new[] {Normalize(R), Normalize(G), Normalize(B)}); }
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
                    default: throw new FacadeException("unsupported GUI value kind");
                }
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
            internal readonly Queue<GuiBackendTarget> PendingDestroys = new Queue<GuiBackendTarget>();
            internal int Screens;
            internal GuiRetainedState Copy()
            {
                var Result = new GuiRetainedState {Screens = Screens};
                foreach (var Value in Nodes) Result.Nodes.Add(Value.Key, Value.Value.Copy());
                foreach (var Value in Connections) Result.Connections.Add(Value.Key, Value.Value);
                foreach (var Value in Presentations) Result.Presentations.Add(Value.Key, Value.Value.Copy());
                foreach (GuiBackendTarget Value in PendingDestroys) Result.PendingDestroys.Enqueue(Value);
                return Result;
            }
        }

        internal sealed class GuiRetainedWorld
        {
            internal readonly GuiLimits Limits;
            internal readonly PlayerDirectory Players;
            internal readonly IGuiBackend Backend;
            internal int LiveObjects { get; private set; }
            internal int LivePresentations { get; private set; }
            private readonly Dictionary<string, int> PlayerPresentations = new Dictionary<string, int>(StringComparer.Ordinal);
            internal GuiRetainedWorld(GuiLimits Limits) : this(Limits, null, new InMemoryGuiBackend()) { }
            internal GuiRetainedWorld(GuiLimits Limits, PlayerDirectory Players, IGuiBackend Backend)
            { this.Limits = Limits ?? throw new ArgumentNullException("Limits"); this.Players = Players; this.Backend = Backend ?? throw new ArgumentNullException("Backend"); }
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
            internal string NewClientPrefix()
            {
                var Bytes = new byte[16]; using (RandomNumberGenerator Random = RandomNumberGenerator.Create()) Random.GetBytes(Bytes);
                return "cluau_" + BitConverter.ToString(Bytes).Replace("-", "").ToLowerInvariant() + "_";
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
            private bool Disposed;

            internal GuiRetainedRegistry(GuiRetainedWorld World, ulong VmGenerationId, ulong DomainLifetimeId)
            {
                if (VmGenerationId == 0 || DomainLifetimeId == 0) throw new ArgumentOutOfRangeException("GUI owner identity");
                this.World = World ?? throw new ArgumentNullException("World"); Limits = World.Limits;
                this.VmGenerationId = VmGenerationId; this.DomainLifetimeId = DomainLifetimeId;
            }
            internal int LiveObjectCount { get { return State.Nodes.Count; } }
            internal int ScreenCount { get { return State.Screens; } }
            internal int ConnectionCount { get { return State.Connections.Count; } }
            internal int PresentationCount { get { return State.Presentations.Count; } }
            internal int PublicationDepth { get { return Publications.Count; } }
            internal bool HasWork {
                get {
                    if (State.PendingDestroys.Count != 0) return true;
                    foreach (GuiPresentation Value in State.Presentations.Values) if (Value.NeedsReplace) return true;
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
                RequireLive(); if (Publications.Count >= 64) throw new FacadeException("GUI publication nesting limit reached");
                Publications.Push(State.Copy());
            }
            internal void CommitPublication()
            { RequireLive(); if (Publications.Count == 0) throw new FacadeException("invalid GUI publication commit"); Publications.Pop(); }
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
                        if (Fields[2] != "Activated" || Value.ClassId != GuiClassId.TextButton) throw new FacadeException("event is not available on GUI object");
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
                switch (Fields[0]) {
                    case "create": {
                        RequireFields(Fields, 3); ulong ParentId = Fields[1].Length == 0 ? 0 : Id(Fields[1]);
                        return ObjectResult(Create(ParentId, Fields[2]));
                    }
                    case "set": if (Fields.Length < 4) throw new FacadeException("invalid GUI property assignment"); SetProperty(Node(Id(Fields[1])), Fields[2], Fields, 3); return new string[0];
                    case "clone": RequireFields(Fields, 2); return ObjectResult(Clone(Node(Id(Fields[1]))));
                    case "destroy": RequireFields(Fields, 2); return Destroy(Id(Fields[1]));
                    case "connect": {
                        RequireFields(Fields, 2); GuiRetainedNode Button = Node(Id(Fields[1]));
                        if (Button.ClassId != GuiClassId.TextButton) throw new FacadeException("Activated is only available on TextButton");
                        int Count = 0; foreach (ulong Owner in State.Connections.Values) if (Owner == Button.Identity.GuiObjectId) Count++;
                        if (Count >= Limits.MaxSignalConnectionsPerButton || State.Connections.Count >= Limits.MaxGuiSignalConnectionsPerDomain)
                            throw new FacadeException("GUI Signal connection limit reached");
                        string Registration = NextRegistration(); State.Connections.Add(Registration, Button.Identity.GuiObjectId); return new[] {Registration};
                    }
                    case "disconnect": {
                        RequireFields(Fields, 3); ulong ObjectId = Id(Fields[1]); ulong Owner;
                        if (State.Connections.TryGetValue(Fields[2], out Owner) && Owner == ObjectId) State.Connections.Remove(Fields[2]);
                        return new string[0];
                    }
                    case "show": RequireFields(Fields, 4); Show(Node(Id(Fields[1])), Fields[2], Fields[3]); return new string[0];
                    case "hide": RequireFields(Fields, 4); Hide(Node(Id(Fields[1])), Fields[2], Fields[3]); return new string[0];
                    default: throw new FacadeException("unknown GUI mutation");
                }
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
                if (Parent != null) ValidateAttachment(null, Descriptor.Id, Parent, 1, 1, Descriptor.Id == GuiClassId.TextButton ? 1 : 0, TextBytes(Descriptor.Id));
                if (NextObjectId == ulong.MaxValue) throw new FacadeException("GUI object identity exhausted");
                World.Adjust(1); ulong ObjectId = NextObjectId++;
                var Result = new GuiRetainedNode(new GuiObjectIdentity(VmGenerationId, DomainLifetimeId, ObjectId), Descriptor.Id);
                ApplyDefaults(Result); State.Nodes.Add(ObjectId, Result); if (Descriptor.Id == GuiClassId.ScreenGui) State.Screens++;
                if (Parent != null) { Attach(Result, Parent); MarkScreenForFull(RootScreen(Parent)); }
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
                State.Screens += ScreenCopies; return Map[Source.Identity.GuiObjectId];
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
                World.Adjust(-RemovedIds.Count); if (AffectedScreen != null && Root.ClassId != GuiClassId.ScreenGui) MarkScreenForFull(AffectedScreen);
                return RemovedConnections.ToArray();
            }

            private void SetProperty(GuiRetainedNode Node, string Name, string[] Fields, int KindIndex)
            {
                GuiPropertyUse Use;
                if (!GuiSchema.TryGetProperty(Node.ClassId, Name, out Use) || !Use.Writable) throw new FacadeException("GUI property is unknown or read-only");
                if (Use.Descriptor.Id == GuiPropertyId.Parent) { SetParent(Node, Fields, KindIndex); return; }
                GuiStoredValue Value = ParseValue(Use.Descriptor, Fields, KindIndex);
                if (Use.Descriptor.Id == GuiPropertyId.Text) {
                    GuiRetainedNode Screen = RootScreen(Node); if (Screen != null) {
                        int Current = ScreenTextBytes(Screen); int Old = FacadePolicy.Utf8.GetByteCount(Node.Properties[GuiPropertyId.Text].Text);
                        int Next = FacadePolicy.Utf8.GetByteCount(Value.Text);
                        if (Current - Old + Next > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
                    }
                }
                Node.Properties[Use.Descriptor.Id] = Value;
                if (Use.Descriptor.MutationKind == GuiMutationKind.Structural) MarkScreenForFull(RootScreen(Node));
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
                if (Parent != null) ValidateAttachment(Child, Child.ClassId, Parent, Objects, Depth, Buttons, TextBytes);
                GuiRetainedNode OldScreen = RootScreen(Child), NewScreen = Parent == null ? null : RootScreen(Parent);
                if (Child.ParentId.HasValue) State.Nodes[Child.ParentId.Value].Children.Remove(Child.Identity.GuiObjectId);
                Child.ParentId = null; if (Parent != null) Attach(Child, Parent);
                MarkScreenForFull(OldScreen); if (NewScreen != OldScreen) MarkScreenForFull(NewScreen);
            }

            private void Show(GuiRetainedNode Screen, string PlayerToken, string PlayerUserId)
            {
                RequireScreen(Screen); if (World.Resolve(PlayerToken, PlayerUserId) == null) throw new FacadeException("Player is no longer connected");
                string Key = PresentationKey(Screen.Identity.GuiObjectId, PlayerToken); GuiPresentation Existing;
                if (State.Presentations.TryGetValue(Key, out Existing)) {
                    if (Existing.SynchronizationUncertain) Existing.NeedsReplace = true;
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
                State.Presentations.Remove(Key); World.RemovePresentation(Presentation.PlayerToken);
                if (Presentation.WasSent) State.PendingDestroys.Enqueue(Presentation.Target);
            }
            internal void Disconnect(PlayerLifetime Player)
            {
                RequireLive(); if (Player == null) return;
                var Keys = new List<string>(); foreach (var Value in State.Presentations) if (Value.Value.PlayerToken == Player.Token) Keys.Add(Value.Key);
                foreach (string Key in Keys) { World.RemovePresentation(State.Presentations[Key].PlayerToken); State.Presentations.Remove(Key); }
                if (State.PendingDestroys.Count != 0) {
                    var Keep = new Queue<GuiBackendTarget>(); while (State.PendingDestroys.Count != 0) {
                        GuiBackendTarget Target = State.PendingDestroys.Dequeue(); if (Target.ExactPlayerConnectionToken != Player.Token) Keep.Enqueue(Target);
                    }
                    while (Keep.Count != 0) State.PendingDestroys.Enqueue(Keep.Dequeue());
                }
            }
            internal int FlushOne(int RemainingBytes)
            {
                RequireLive(); if (Publications.Count != 0) return 0;
                if (State.PendingDestroys.Count != 0) { World.Backend.Destroy(State.PendingDestroys.Dequeue()); return 0; }
                string SelectedKey = null; GuiPresentation Selected = null;
                foreach (var Value in State.Presentations) if (Value.Value.NeedsReplace && (SelectedKey == null || StringComparer.Ordinal.Compare(Value.Key, SelectedKey) < 0)) {
                    SelectedKey = Value.Key; Selected = Value.Value;
                }
                if (Selected == null) return 0;
                if (World.Resolve(Selected.PlayerToken, Selected.PlayerUserId) == null) {
                    State.Presentations.Remove(SelectedKey); World.RemovePresentation(Selected.PlayerToken); return 0;
                }
                GuiRetainedNode Screen;
                if (!State.Nodes.TryGetValue(Selected.ScreenId, out Screen)) {
                    State.Presentations.Remove(SelectedKey); World.RemovePresentation(Selected.PlayerToken); return 0;
                }
                GuiRenderPlan Plan;
                try { Plan = GuiRenderCompiler.Compile(State, Screen, Selected, Limits); }
                catch { Selected.NeedsReplace = false; Selected.SynchronizationUncertain = true; return 0; }
                if (Plan.EstimatedSerializedBytes > RemainingBytes) return -Plan.EstimatedSerializedBytes;
                GuiBackendResult Result = World.Backend.Replace(Selected.Target, Plan);
                if (Result.Code == GuiBackendResultCode.TargetUnavailable) {
                    State.Presentations.Remove(SelectedKey); World.RemovePresentation(Selected.PlayerToken);
                } else if (Result.Accepted) {
                    Selected.WasSent = true; Selected.NeedsReplace = false; Selected.SynchronizationUncertain = false;
                } else { Selected.NeedsReplace = false; Selected.SynchronizationUncertain = true; }
                return Plan.EstimatedSerializedBytes;
            }
            private void RemovePresentationsForScreen(ulong ScreenId)
            {
                var Keys = new List<string>(); foreach (var Value in State.Presentations) if (Value.Value.ScreenId == ScreenId) Keys.Add(Value.Key);
                foreach (string Key in Keys) {
                    GuiPresentation Presentation = State.Presentations[Key]; State.Presentations.Remove(Key); World.RemovePresentation(Presentation.PlayerToken);
                    if (Presentation.WasSent) State.PendingDestroys.Enqueue(Presentation.Target);
                }
            }
            private void MarkScreenForFull(GuiRetainedNode Screen)
            {
                if (Screen == null) return;
                foreach (GuiPresentation Value in State.Presentations.Values) if (Value.ScreenId == Screen.Identity.GuiObjectId) Value.NeedsReplace = true;
            }
            private static void RequireScreen(GuiRetainedNode Screen)
            { if (Screen.ClassId != GuiClassId.ScreenGui) throw new FacadeException("Show, Hide and IsShown require ScreenGui"); }
            private static string PresentationKey(ulong ScreenId, string PlayerToken)
            { return ScreenId.ToString(CultureInfo.InvariantCulture) + ":" + PlayerToken; }

            private void ValidateAttachment(GuiRetainedNode Child, GuiClassId ChildClass, GuiRetainedNode Parent, int Objects, int Depth, int Buttons, int TextBytes)
            {
                if (!GuiSchema.GetClass(Parent.ClassId).CanHaveChildren) throw new FacadeException("GUI parent cannot have children");
                if (Parent.Children.Count >= Limits.MaxChildrenPerObject) throw new FacadeException("GUI child limit reached");
                for (GuiRetainedNode Cursor = Parent; Cursor != null; Cursor = Cursor.ParentId.HasValue ? State.Nodes[Cursor.ParentId.Value] : null)
                    if (Child != null && Cursor.Identity.GuiObjectId == Child.Identity.GuiObjectId) throw new FacadeException("GUI parent cycle rejected");
                int ParentDepth = DepthFromRoot(Parent);
                if (ParentDepth + Depth > Limits.MaxTreeDepth) throw new FacadeException("GUI tree depth limit reached");
                GuiRetainedNode Screen = RootScreen(Parent);
                if (Screen != null) {
                    int ExistingObjects = SubtreeCount(Screen); int ExistingButtons = SubtreeButtons(Screen); int ExistingText = SubtreeTextBytes(Screen);
                    GuiRetainedNode OldScreen = Child == null ? null : RootScreen(Child);
                    bool SameScreen = OldScreen != null && OldScreen.Identity.GuiObjectId == Screen.Identity.GuiObjectId;
                    if (!SameScreen && ExistingObjects + Objects > Limits.MaxObjectsPerScreen) throw new FacadeException("ScreenGui object limit reached");
                    if (!SameScreen && ExistingButtons + Buttons > Limits.MaxButtonsPerScreen) throw new FacadeException("ScreenGui button limit reached");
                    if (!SameScreen && ExistingText + TextBytes > Limits.MaxTextUtf8BytesPerScreen) throw new FacadeException("ScreenGui aggregate text limit reached");
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
                Node.Properties[GuiPropertyId.Position] = GuiStoredValue.UDim2(0, 0, 0, 0);
                int Height = Node.ClassId == GuiClassId.Frame ? 100 : Node.ClassId == GuiClassId.TextLabel ? 30 : 36;
                Node.Properties[GuiPropertyId.Size] = GuiStoredValue.UDim2(0, 100, 0, Height);
                Node.Properties[GuiPropertyId.AnchorPoint] = GuiStoredValue.Vector2(0, 0);
                Node.Properties[GuiPropertyId.Visible] = GuiStoredValue.Bool(true);
                Node.Properties[GuiPropertyId.BackgroundColor3] = GuiStoredValue.Color3(1, 1, 1);
                Node.Properties[GuiPropertyId.BackgroundTransparency] = GuiStoredValue.Number(Node.ClassId == GuiClassId.TextLabel ? 1 : 0);
                Node.Properties[GuiPropertyId.ZIndex] = GuiStoredValue.Int(1);
                if (Node.ClassId == GuiClassId.TextLabel || Node.ClassId == GuiClassId.TextButton) {
                    Node.Properties[GuiPropertyId.Text] = GuiStoredValue.String(""); Node.Properties[GuiPropertyId.TextColor3] = GuiStoredValue.Color3(0, 0, 0);
                    Node.Properties[GuiPropertyId.TextTransparency] = GuiStoredValue.Number(0); Node.Properties[GuiPropertyId.TextSize] = GuiStoredValue.Int(14);
                    Node.Properties[GuiPropertyId.TextXAlignment] = GuiStoredValue.String("Center"); Node.Properties[GuiPropertyId.TextYAlignment] = GuiStoredValue.String("Center");
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
            private int SubtreeButtons(GuiRetainedNode Root) { int Result = Root.ClassId == GuiClassId.TextButton ? 1 : 0; foreach (ulong Child in Root.Children) Result += SubtreeButtons(State.Nodes[Child]); return Result; }
            private int SubtreeTextBytes(GuiRetainedNode Root)
            {
                int Result = 0; GuiStoredValue Text; if (Root.Properties.TryGetValue(GuiPropertyId.Text, out Text)) Result = FacadePolicy.Utf8.GetByteCount(Text.Text);
                foreach (ulong Child in Root.Children) Result += SubtreeTextBytes(State.Nodes[Child]); return Result;
            }
            private int TextBytes(GuiClassId ClassId) { return 0; }
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
                var Destroyed = new HashSet<string>(StringComparer.Ordinal);
                foreach (GuiPresentation Value in State.Presentations.Values) {
                    World.RemovePresentation(Value.PlayerToken);
                    if (Value.WasSent && Destroyed.Add(Value.Target.Key)) TryDestroy(Value.Target);
                }
                foreach (GuiBackendTarget Value in State.PendingDestroys) if (Destroyed.Add(Value.Key)) TryDestroy(Value);
                World.Adjust(-State.Nodes.Count); State = new GuiRetainedState(); Publications.Clear();
            }
            private void TryDestroy(GuiBackendTarget Target)
            { try { World.Backend.Destroy(Target); } catch { } }
        }
    }
}
